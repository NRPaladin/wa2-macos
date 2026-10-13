# SPDX-License-Identifier: MIT
"""Extract the first Windows icon group without executing the PE (requires pefile, Pillow)."""
import argparse, hashlib, io, json, struct
from pathlib import Path
import pefile
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('exe', type=Path)
parser.add_argument('output', type=Path)
a = parser.parse_args()
pe = pefile.PE(str(a.exe))
resources = {}
for kind in pe.DIRECTORY_ENTRY_RESOURCE.entries:
    if kind.id in (3, 14):
        for entry in kind.directory.entries:
            data = entry.directory.entries[0].data.struct
            resources[kind.id, entry.id] = pe.get_data(data.OffsetToData, data.Size)
group_id, group = next((key[1], data) for key, data in resources.items() if key[0] == 14)
count = struct.unpack_from('<H', group, 4)[0]
header = bytearray(struct.pack('<HHH', 0, 1, count))
payload = bytearray()
sizes = []
for i in range(count):
    w, h, colors, reserved, planes, bits, length, icon_id = struct.unpack_from('<BBBBHHIH', group, 6 + 14*i)
    icon = resources[3, icon_id]
    assert len(icon) == length
    header.extend(struct.pack('<BBBBHHII', w, h, colors, reserved, planes, bits, length, 6+16*count+len(payload)))
    payload.extend(icon)
    sizes.append([w or 256, h or 256])
a.output.mkdir(parents=True, exist_ok=True)
ico = bytes(header + payload)
(a.output/'wa2.ico').write_bytes(ico)
image = Image.open(io.BytesIO(ico)).convert('RGBA')
image.save(a.output/'wa2.png')
# Native sizes retain the original pixels; larger Retina representations are resampled.
iconset = a.output/'wa2.iconset'
iconset.mkdir(exist_ok=True)
original = Image.open(io.BytesIO(ico))
for logical in (16, 32, 128, 256, 512):
    for scale in (1, 2):
        size = logical*scale
        frame = original.ico.getimage((size, size)).convert('RGBA') if (size,size) in original.ico.sizes() else image.resize((size,size), Image.Resampling.LANCZOS)
        frame.save(iconset/f'icon_{logical}x{logical}{"@2x" if scale==2 else ""}.png')
report = {'source': a.exe.name, 'source_sha256': hashlib.sha256(a.exe.read_bytes()).hexdigest(), 'group_id': group_id, 'original_sizes': sizes, 'largest_original': list(image.size), 'method': 'PE resource extraction; no EXE execution; no redrawing', 'ico_sha256': hashlib.sha256(ico).hexdigest()}
(a.output/'provenance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
print(json.dumps(report, ensure_ascii=False, indent=2))
