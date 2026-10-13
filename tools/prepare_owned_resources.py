#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Prepare ignored local resources from a user's own supported game installation."""
import argparse
import shutil
import subprocess
import sys
from pathlib import Path

ARCHIVES = ['BGM.PAK', 'SE.PAK', 'VOICE.PAK', 'bak.pak', 'char.pak', 'ck-gal.pak',
            'fnt.pak', 'fon.pak', 'grp.pak', 'script.pak', 'IC/BGM.PAK', 'IC/SE.PAK',
            'IC/VOICE.PAK', 'IC/bak.pak', 'IC/char.pak', 'IC/grp.pak', 'IC/script.pak']

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True)
    parser.add_argument('--exe', type=Path, required=True, help='Owned Windows EXE; read for icon extraction only')
    parser.add_argument('--ffmpeg', type=Path, required=True, help='FFmpeg with libtheora/libvorbis; ffprobe alongside it')
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1]/'WA2-macOS-local/source')
    args = parser.parse_args()
    game, project = args.game.resolve(), args.project.resolve()
    target = project/'Wa2Res'
    if target == game or game in target.parents or target in game.parents:
        parser.error('Game and generated resource directory must be separate')
    missing = [name for name in ARCHIVES if not (game/name).is_file()]
    if missing:
        parser.error('Unsupported/incomplete game data: ' + ', '.join(missing))
    if not args.exe.is_file() or not args.ffmpeg.is_file():
        parser.error('Windows EXE and FFmpeg paths must point to existing files')
    if target.exists():
        parser.error('Refusing to overwrite an existing Wa2Res directory; move it aside first')
    target.mkdir(parents=True)
    for name in ARCHIVES:
        destination = target/name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(game/name, destination)
    if (game/'grp').is_dir():
        shutil.copytree(game/'grp', target/'grp')
    scripts = project/'tools'
    subprocess.run([sys.executable, str(scripts/'prepare_assets.py'), str(target), '--project', str(project)], check=True)
    subprocess.run([sys.executable, str(scripts/'prepare_movies.py'), str(game), str(target/'movies'), '--ffmpeg', str(args.ffmpeg), '--jobs', '1'], check=True)
    icons = project/'assets/app-icon'
    subprocess.run([sys.executable, str(scripts/'extract_windows_icon.py'), str(args.exe), str(icons)], check=True)
    subprocess.run(['iconutil', '-c', 'icns', str(icons/'wa2.iconset'), '-o', str(icons/'wa2.icns')], check=True)
    subtitles = project/'assets/sub.yaml'
    if not subtitles.exists():
        subtitles.write_text('soundSubtitle: []\nvoiceSubtitle: []\n')
    print('Prepared local resources: ' + str(target))
    print('Game media, icon and subtitle data remain excluded from Git.')

if __name__ == '__main__':
    main()
