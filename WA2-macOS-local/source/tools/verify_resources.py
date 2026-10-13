# SPDX-License-Identifier: MIT
"""Hash/check all local resources and optionally decode every converted movie silently."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import subprocess
from pak import index
from prepare_movies import MOVIES, RECIPE


def sha(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def verify(root, original=None, ffmpeg=None):
    report = {'archives': [], 'movies': [], 'loose_images': [], 'original_saves': 'never accessed by runtime tests'}
    archives = sorted(p for p in root.rglob('*') if p.is_file() and p.suffix.lower() == '.pak')
    for path in archives:
        relative = path.relative_to(root).as_posix()
        digest = sha(path)
        if original:
            assert digest == sha(original / relative), f'Copy mismatch: {relative}'
        entries = index(path)
        report['archives'].append({'path': relative, 'entries': len(entries), 'bytes': path.stat().st_size, 'sha256': digest})
    scripts = index(root / 'ck-gal.pak')
    for chapter in [b'1001', b'2001', b'3001']:
        assert chapter + b'.bnr' in scripts and chapter + b'.txt' in scripts
    report['script_pairs'] = sum(k.endswith(b'.bnr') for k in scripts)
    manifest = json.loads((root / 'movies/movies.json').read_text())
    assert set(manifest) == set(MOVIES), 'Incomplete movie manifest'
    for name, item in sorted(manifest.items()):
        path = root / 'movies' / (name + '.ogv')
        assert item['recipe'] == RECIPE and item['fps'] == 30
        assert item['sha256'] == sha(path) and item['bytes'] == path.stat().st_size
        if original:
            assert item['source_sha256'] == sha(original / item['source'])
        report['movies'].append({'path': 'movies/' + path.name, 'sha256': item['sha256'], 'duration': item['duration']})
    for path in sorted((root / 'grp').glob('*.tga')):
        relative = path.relative_to(root).as_posix()
        digest = sha(path)
        if original:
            assert digest == sha(original / relative)
        report['loose_images'].append({'path': relative, 'sha256': digest})
    if ffmpeg:
        def decode(movie):
            subprocess.run([str(ffmpeg), '-nostdin', '-v', 'error', '-xerror', '-threads', '1', '-i', str(root / movie['path']), '-f', 'null', '-'],
                           check=True, capture_output=True, timeout=600)
            print('DECODE PASS ' + movie['path'], flush=True)
        with ThreadPoolExecutor(max_workers=3) as pool:
            list(pool.map(decode, report['movies']))
        report['all_movies_fully_decoded'] = True
    else:
        report['all_movies_fully_decoded'] = False
    report['result'] = 'PASS'
    return report


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('resources', type=Path)
    p.add_argument('--original', type=Path)
    p.add_argument('--ffmpeg', type=Path)
    p.add_argument('--report', type=Path, required=True)
    args = p.parse_args()
    report = verify(args.resources, args.original, args.ffmpeg)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(f"PASS: {len(report['archives'])} archives, {len(report['movies'])} movies, {report['script_pairs']} scripts", flush=True)
