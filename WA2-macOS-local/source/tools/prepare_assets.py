# SPDX-License-Identifier: MIT
"""Recover Godot import assets from a local, user-owned WA2 installation."""
import argparse
import io
import json
import re
import struct
from pathlib import Path
from PIL import Image
from pak import index, read


def tga_image(data):
    # Pillow cannot decode 32-bit TGA palettes; preserve their alpha explicitly.
    ident, cmap, kind, first, count, bits, x, y, w, h, depth, flags = struct.unpack_from('<BBBHHBHHHHBB', data)
    if cmap == 1 and kind == 1 and bits == 32 and depth == 8:
        start = 18 + ident
        palette = data[start:start + count * 4]
        pixels = data[start + count * 4:start + count * 4 + w * h]
        import numpy as np
        colors = np.frombuffer(palette, dtype=np.uint8).reshape(count, 4)[:, [2, 1, 0, 3]]
        a = colors[np.frombuffer(pixels, dtype=np.uint8).reshape(h, w) - first]
        if not flags & 32:
            a = a[::-1]
        if flags & 16:
            a = a[:, ::-1]
        return Image.fromarray(a, 'RGBA')
    return Image.open(io.BytesIO(data)).convert('RGBA')


def prepare(root, project):
    entries = {}
    for name in ['IC/grp.pak', 'grp.pak', 'IC/SE.PAK', 'SE.PAK']:
        entries.update({k.decode('cp932').lower(): v for k, v in index(root / name).items()})
    assets = project / 'assets'
    outputs = []
    # Recover all UI/effect atlases; .ani files contain additional runtime names.
    for name, entry in entries.items():
        suffix = Path(name).suffix.lower()
        if suffix not in ('.tga', '.ani'):
            continue
        loose = root / 'grp' / name
        data = loose.read_bytes() if loose.is_file() else read(entry)
        out = assets / 'grp' / (Path(name).stem + ('.png' if suffix == '.tga' else '.ani'))
        out.parent.mkdir(parents=True, exist_ok=True)
        if suffix == '.tga':
            tga_image(data).save(out)
        else:
            out.write_bytes(data)
        outputs.append(str(out.relative_to(project)))
    # Verified against the user's executables: WA2_chs.exe opens "fon", WA2.exe "fnt".
    # Chinese text uses substituted Shift-JIS glyph slots, not Unicode Chinese.
    for name, lang in [('fon.pak', 'cn'), ('fnt.pak', 'jp')]:
        for key, entry in index(root / name).items():
            decoded = key.decode('cp932')
            if decoded not in ('本体80.tga', '袋影80.tga'):
                continue
            out = assets / 'fonts' / lang / (Path(decoded).stem + '.png')
            out.parent.mkdir(parents=True, exist_ok=True)
            tga_image(read(entry)).save(out)
            outputs.append(str(out.relative_to(project)))
    # Preserve the spelling used by scenes on case-sensitive APFS.
    refs = set()
    for folder, glob in [('scene', '*.tscn'), ('script', '*.cs'), ('assets/ani', '*.tres')]:
        for path in (project / folder).glob(glob):
            refs.update(re.findall(r'res://(assets/[^"\n]+)', path.read_text(encoding='utf-8')))
    refs.update(re.findall(r'res://(assets/[^"\n]+)', (project / 'main.tscn').read_text(encoding='utf-8')))
    for ref in sorted(refs):
        out = project / ref
        if ref.lower().endswith('.wav'):
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_bytes(read(entries[out.name.lower()]))
            outputs.append(ref)
        elif out.suffix == '.png' and '/grp/' in ref:
            canonical = assets / 'grp' / out.name.lower()
            if canonical.is_file() and canonical.name != out.name:
                # On Windows rename through a temporary name to change the actual case.
                temp = canonical.with_suffix('.case-temp')
                canonical.rename(temp)
                temp.rename(out)
    report = {'source': str(root), 'generated_count': len(outputs), 'outputs': sorted(outputs)}
    (project / 'build').mkdir(exist_ok=True)
    (project / 'build/asset-preparation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'Recovered {len(outputs)} local assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('game', type=Path)
    parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    prepare(args.game.resolve(), args.project.resolve())
