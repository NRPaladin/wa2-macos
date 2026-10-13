# Component licenses and source

| Component | License | Included material / retained notice |
| --- | --- | --- |
| dorakyuraduang/wa2-godot implementation | Apache-2.0 | Source/configuration within the scope of source/PORTING_CODE.md; source/LICENSES/Apache-2.0.txt and source/NOTICE retained |
| Locally added preparation and verification tools | MIT | Root LICENSE defines the exact scope; LICENSES/MIT.txt and per-file SPDX markers |
| Noto Sans CJK SC Regular | SIL OFL 1.1 | One font file, OFL.txt and SOURCE.md in source/assets/fonts/unicode; font hash verified |
| Godot 4.5.1 .NET | MIT and upstream component notices | Engine/template downloaded separately; source/LICENSES/Godot-4.5.1-MIT.txt and Godot-4.5.1-COPYRIGHT.txt retained |
| YamlDotNet 16.3.0 | MIT | Restored as a package dependency; source/LICENSES/YamlDotNet-MIT.txt retained |
| .NET 9 | MIT and third-party notices | SDK/runtime supplied separately during build/export; source/LICENSES/DotNet-9-LICENSE.txt and DotNet-9-ThirdPartyNotices.txt retained |
| Pillow, NumPy, pefile | Their respective licenses | Installed separately for local preparation; no package source/binaries bundled |
| FFmpeg | Depends on the user's selected build | Invoked as a separately installed local conversion tool; no FFmpeg binary or WMV plugin bundled in this repository |

Here `source/` means `WA2-macOS-local/source/`.
The existing upstream component notices remain at source/THIRD_PARTY_NOTICES.md;
references there to a WMV plugin describe upstream components, which this macOS
source snapshot does not include. This snapshot uses Godot's built-in Theora player.

## Excluded game material

Game archives, scenario data, translations, UI/effect images, original fonts,
voice/BGM/sound effects, converted movies, extracted original icons and optional
mixed-source subtitles are excluded. See source/ASSET_POLICY.md and
source/SUBTITLE_NOTICE.md. A code license does not grant permission for this data.

## Redistribution scope

Preserve the existing component copyright and license notices. Apply MIT only to
locally authored files explicitly identified by the root LICENSE/SPDX markers;
retain Apache-2.0 for upstream implementation and modifications in its scope.
The locally generated full app/installer contains private game resources and is
not an approved repository or Release upload.
