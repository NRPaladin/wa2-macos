# SPDX-License-Identifier: MIT
"""Convert the 16 movies used by WA2 to portable Theora/Vorbis. Local use only."""
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
import subprocess
from pathlib import Path

MOVIES = {f'mv{i:02}': f'mv{i:02}0.pak' for i in [0, 10, 11, 12, 13, 14, 20, 21, 22, 23, 24]}
MOVIES.update({f'mv{i:02}': f'IC/{"MV" if i >= 7 else "mv"}{i:02}0.pak' for i in [1, 2, 7, 8, 9]})
RECIPE = 'theora-q7-speed2-fps30-vorbis-q5-singledecode-async1-v3'

def probe(tool, path):
    return json.loads(subprocess.check_output([str(tool), '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]))

def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def convert(args, key, source, previous):
    ffprobe = args.ffmpeg.with_name('ffprobe' + args.ffmpeg.suffix)
    src = args.game / source
    out = args.output / (key + '.ogv')
    source_info = probe(ffprobe, src)
    source_hash = digest(src)
    old = previous.get(key, {})
    if not (out.exists() and old.get('recipe') == RECIPE and old.get('source_sha256') == source_hash and old.get('sha256') == digest(out)):
        tmp = out.with_suffix('.partial.ogv')
        print(f'Converting {source} -> {out.name}', flush=True)
        with (args.output / (key + '.conversion.log')).open('w', encoding='utf-8') as log:
            subprocess.run([str(args.ffmpeg), '-nostdin', '-y', '-v', 'warning', '-threads', '1', '-i', str(src),
                '-map', '0:v:0', '-map', '0:a:0?', '-c:v', 'libtheora', '-speed_level', '2', '-q:v', '7',
                '-vf', 'fps=30', '-pix_fmt', 'yuv420p', '-c:a', 'libvorbis', '-q:a', '5', '-af', 'aresample=async=1:first_pts=0',
                '-progress', str(args.output / (key + '.progress')), str(tmp)], check=True, stderr=log)
        tmp.replace(out)
    info = probe(ffprobe, out)
    duration = float(info['format']['duration'])
    source_duration = float(source_info['format']['duration'])
    if abs(duration - source_duration) > 1.5:
        raise ValueError(f'Duration mismatch for {key}: {duration} vs {source_duration}')
    vs = next(s for s in info['streams'] if s['codec_type'] == 'video')
    original = next(s for s in source_info['streams'] if s['codec_type'] == 'video')
    if vs['codec_name'] != 'theora' or (vs['width'], vs['height']) != (original['width'], original['height']):
        raise ValueError(f'Wrong codec or dimensions in {out}')
    if any(s['codec_type'] == 'audio' for s in source_info['streams']) and not any(s['codec_name'] == 'vorbis' for s in info['streams']):
        raise ValueError(f'Audio missing in {out}')
    rate_a, rate_b = map(int, vs['r_frame_rate'].split('/'))
    result = {'source': source, 'source_sha256': source_hash, 'duration': duration, 'source_duration': source_duration,
              'fps': rate_a / rate_b, 'width': vs['width'], 'height': vs['height'],
              'bytes': out.stat().st_size, 'sha256': digest(out), 'recipe': RECIPE}
    print(f'OK {key}: {duration:.2f}s, {vs["width"]}x{vs["height"]}', flush=True)
    return key, result

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('game', type=Path)
    p.add_argument('output', type=Path)
    p.add_argument('--ffmpeg', required=True, type=Path)
    p.add_argument('--jobs', type=int, default=3)
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    manifest = args.output / 'movies.json'
    previous = json.loads(manifest.read_text()) if manifest.exists() else {}
    report = {}
    # Short IC opening first, then the other chapters; each worker has a separate output.
    order = ['mv00', 'mv01', 'mv10', 'mv02', *[k for k in MOVIES if k not in ('mv00','mv01','mv10','mv02')]]
    with ThreadPoolExecutor(max_workers=max(1, min(args.jobs, 8))) as pool:
        futures = [pool.submit(convert, args, k, MOVIES[k], previous) for k in order]
        for future in as_completed(futures):
            key, result = future.result()
            report[key] = result
            tmp = manifest.with_suffix('.partial.json')
            tmp.write_text(json.dumps(report, indent=2, sort_keys=True), encoding='utf-8')
            tmp.replace(manifest)
    print(f'Validated {len(report)} movies', flush=True)

if __name__ == '__main__':
    main()
