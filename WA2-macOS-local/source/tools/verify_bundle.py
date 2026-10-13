# SPDX-License-Identifier: MIT
"""Verify the exported Mac zip structurally. This does not run macOS code."""
import argparse
import json
import plistlib
import struct
import zipfile
from pathlib import Path


def verify(path):
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None, 'Zip CRC failure'
        names = set(z.namelist())
        prefix = 'WHITE ALBUM2.app/Contents/'
        plist = plistlib.loads(z.read(prefix + 'Info.plist'))
        assert plist['CFBundleIdentifier'] == 'local.wa2.godot'
        assert plist['LSMinimumSystemVersionByArchitecture'] == {'arm64': '12.0', 'x86_64': '12.0'}
        executable = prefix + 'MacOS/' + plist['CFBundleExecutable']
        info = z.getinfo(executable)
        assert info.create_system == 3 and (info.external_attr >> 16) & 0o111, 'Missing Unix executable permission'
        binary = z.read(executable)
        assert binary[:4] == b'\xca\xfe\xba\xbe', 'Expected Universal 2 executable'
        count, = struct.unpack_from('>I', binary, 4)
        architectures = [struct.unpack_from('>I', binary, 8 + i * 20)[0] for i in range(count)]
        assert set(architectures) == {0x1000007, 0x100000C}, architectures
        for arch in ['arm64', 'x86_64']:
            runtime = prefix + f'Resources/data_wa2_macos_{arch}/'
            for required in ['wa2.dll', 'wa2.runtimeconfig.json', 'libhostfxr.dylib', 'libhostpolicy.dylib', 'libcoreclr.dylib', 'System.Private.CoreLib.dll']:
                assert runtime + required in names, f'Missing {arch}: {required}'
            assert len(z.read(runtime + 'wa2.dll')) > 100000
            config = json.loads(z.read(runtime + 'wa2.runtimeconfig.json'))
            assert config['runtimeOptions']['tfm'] == 'net9.0'
        assert prefix + 'Resources/WHITE ALBUM2.pck' in names
        assert prefix + '_CodeSignature/CodeResources' in names
        assert not any(n.lower().endswith(('.sav', '.exe', '.pak')) for n in names), 'Unexpected original game, save or platform binary'
        return {'zip': str(path), 'entries': len(names), 'bytes': path.stat().st_size,
                'architectures': ['arm64', 'x86_64'], 'embedded_dotnet': 'net9.0',
                'executable_mode': oct(info.external_attr >> 16), 'crc': 'pass',
                'macos_native_execution': 'NOT RUN (requires Mac)', 'result': 'PASS'}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('zip', type=Path)
    p.add_argument('--report', type=Path)
    args = p.parse_args()
    result = json.dumps(verify(args.zip), indent=2)
    if args.report:
        args.report.write_text(result + '\n', encoding='utf-8')
    print(result)
