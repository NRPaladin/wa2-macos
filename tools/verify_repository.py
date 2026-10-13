#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Check public source candidates or staged blobs; never modify Git history."""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path, PurePosixPath

SOURCE = 'WA2-macOS-local/source/'
NAME = 'NRPaladin'
EMAIL = '171237289+NRPaladin@users.noreply.github.com'
ROOT_FILES = {'.gitignore', '.gitattributes', 'README.md', 'LICENSE', 'NOTICE', 'CHANGES.md',
              'THIRD_PARTY_NOTICES.md', 'LICENSES/MIT.txt', '.githooks/pre-commit',
              'tools/verify_repository.py', 'tools/prepare_owned_resources.py'}
SOURCE_FILES = {'LICENSE', 'NOTICE', 'ASSET_POLICY.md',
                'PORTING_CODE.md', 'SUBTITLE_NOTICE.md', 'THIRD_PARTY_NOTICES.md',
                'NuGet.Config', 'global.json', 'project.godot', 'export_presets.cfg',
                'main.tscn', 'default_bus_layout.tres', 'wa2.csproj', 'assets/font.map',
                'assets/fonts/unicode/NotoSansCJKsc-Regular.otf',
                'assets/fonts/unicode/OFL.txt', 'assets/fonts/unicode/SOURCE.md'}
TOOLS = {'pak.py', 'prepare_assets.py', 'prepare_movies.py', 'extract_windows_icon.py',
         'build_macos.sh', 'play_muted.sh', 'test_macos.sh', 'verify_bundle.py', 'verify_resources.py'}

def git(*args, check=True):
    return subprocess.run(['git', *args], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=check)

def permitted(name):
    if name in ROOT_FILES:
        return True
    if not name.startswith(SOURCE):
        return False
    relative = name[len(SOURCE):]
    path = PurePosixPath(relative)
    if any(part in ('.godot', '.tools', 'build', 'dist', '__pycache__') for part in path.parts):
        return False
    return (relative in SOURCE_FILES or
            relative.startswith('LICENSES/') and path.suffix in ('.txt', '.md') or
            relative.startswith('script/') and path.suffix == '.cs' or
            relative.startswith('scene/') and path.suffix == '.tscn' or
            relative.startswith('shader/') and path.suffix == '.gdshader' or
            relative.startswith('assets/ani/') and path.suffix == '.tres' or
            relative.startswith('tools/') and path.name in TOOLS and len(path.parts) == 2)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staged', action='store_true')
    parser.add_argument('--check-identity', action='store_true')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    flags = ['--cached'] if args.staged else ['--cached', '--others', '--exclude-standard']
    names = sorted(set(git('ls-files', *flags, '-z').stdout.decode().strip('\0').split('\0')) - {''})
    failures = []
    total_bytes = 0
    for name in names:
        if not permitted(name):
            failures.append({'path': name, 'reason': 'outside permitted source components'})
            continue
        if args.staged:
            mode = git('ls-files', '--stage', '--', name).stdout.split(maxsplit=1)[0]
            if mode not in (b'100644', b'100755'):
                failures.append({'path': name, 'reason': 'symlink/submodule is not allowed'})
                continue
            data = git('show', ':' + name).stdout
        else:
            path = Path(name)
            if path.is_symlink():
                failures.append({'path': name, 'reason': 'symlink could expose private data'})
                continue
            data = path.read_bytes()
        total_bytes += len(data)
        if name.endswith('.otf'):
            if hashlib.sha256(data).hexdigest() != '2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b':
                failures.append({'path': name, 'reason': 'font differs from the OFL-licensed file recorded in SOURCE.md'})
            continue
        if b'\0' in data or len(data) > 1024 * 1024:
            failures.append({'path': name, 'reason': 'unexpected binary or large source file'})
        if re.search(rb"(?m)^[^\n=]*=\s*PackedByteArray\(", data) or re.search(rb"(?m)^-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----$", data):
            failures.append({'path': name, 'reason': 'embedded binary/private-key material requires review'})
        if re.search(rb'/Users/[A-Za-z0-9_.-]+/', data):
            failures.append({'path': name, 'reason': 'personal machine path or identity requires review'})
    identities = {}
    if args.check_identity:
        for role in ('AUTHOR', 'COMMITTER'):
            value = git('var', 'GIT_' + role + '_IDENT').stdout.decode().strip()
            identities[role.lower()] = value
            if not value.startswith(NAME + ' <' + EMAIL + '> '):
                failures.append({'role': role, 'reason': 'identity differs from repository privacy settings', 'value': value})
    history = []
    if git('rev-parse', '--verify', 'HEAD', check=False).returncode == 0 or git('for-each-ref', '--format=%(objectname)').stdout.strip():
        for line in git('log', '--all', '--format=%H%x09%an%x09%ae%x09%cn%x09%ce').stdout.decode().splitlines():
            commit, an, ae, cn, ce = line.split('\t')
            if any(email and not email.endswith('@users.noreply.github.com') for email in (ae, ce)) or any(n != NAME for n in (an, cn)):
                history.append({'commit': commit, 'author': {'name': an, 'email': ae}, 'committer': {'name': cn, 'email': ce}, 'note': 'Identity needs human review; history is never rewritten by this tool.'})
    result = {'result': 'PASS' if not failures else 'FAIL', 'scope': 'staged' if args.staged else 'public candidates',
              'files': len(names), 'bytes': total_bytes, 'failures': failures, 'identities': identities,
              'history_identities_to_review': history, 'note': 'Path/content checks supplement license review; a passing scan does not grant copyright permission.'}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return bool(failures)

if __name__ == '__main__':
    raise SystemExit(main())
