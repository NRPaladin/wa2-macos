# macOS changes and source attribution

Upstream: https://github.com/dorakyuraduang/wa2-godot
Baseline: `4fc5a89274d9d0dcfcb2e39ead92353ef1422c4c`.
This repository carries an independent source snapshot; upstream commits and local Codex snapshot refs are not uploaded.
Upstream copyright/license notices are retained separately from local tool licensing.

Local implementation adds portable resource paths, isolated save handling, Theora movie playback,
Unicode interface/subtitle font support, macOS native menus, fullscreen shortcuts,
Enter/keypad Enter dialogue advance, Space UI visibility, and a disabled engine boot splash.
Original proprietary game data is supplied by each user and remains excluded from Git.

The root README, Git policy/guards, license notices, and owned-resource preparation workflow
are local maintenance additions. The root LICENSE identifies their license scope.

## Modified baseline files

Paths below are relative to `WA2-macOS-local/source/`; compare against the pinned upstream baseline.

- `ASSET_POLICY.md`
- `THIRD_PARTY_NOTICES.md`
- `assets/ani/page_01.ani.tres`
- `assets/ani/page_02.ani.tres`
- `assets/ani/スポットライト.ani.tres`
- `assets/ani/ライブシーン.ani.tres`
- `assets/ani/湯煙.ani.tres`
- `assets/ani/煙.ani.tres`
- `assets/ani/雨640x360.ani.tres`
- `export_presets.cfg`
- `main.tscn`
- `project.godot`
- `scene/AdvMain.tscn`
- `scene/BackLog.tscn`
- `scene/BasePage.tscn`
- `scene/BgmModeMenu.tscn`
- `scene/CGModeMenu.tscn`
- `scene/Calender.tscn`
- `scene/LoadSaveMenu.tscn`
- `scene/NovelBackLogMenu.tscn`
- `scene/OptionsMenu.tscn`
- `scene/SavLoadTest.tscn`
- `scene/SceneReplayMenu.tscn`
- `scene/UIConfirm.tscn`
- `scene/VoiceMessage.tscn`
- `scene/back_log_item.tscn`
- `scene/bgm_player.tscn`
- `scene/data_slot.tscn`
- `scene/menu_button.tscn`
- `scene/select_message.tscn`
- `scene/title_menu.tscn`
- `scene/wa2_ani.tscn`
- `scene/wa2_image.tscn`
- `scene/weather.tscn`
- `script/VideoPlayback.cs`
- `script/Wa2AdvMain.cs`
- `script/Wa2EngineMain.cs`

## Locally added Godot-project components

- `LICENSES/DotNet-9-LICENSE.txt`
- `LICENSES/DotNet-9-ThirdPartyNotices.txt`
- `LICENSES/Godot-4.5.1-COPYRIGHT.txt`
- `LICENSES/Godot-4.5.1-MIT.txt`
- `NuGet.Config`
- `assets/fonts/unicode/NotoSansCJKsc-Regular.otf`
- `assets/fonts/unicode/OFL.txt`
- `assets/fonts/unicode/SOURCE.md`
- `global.json`
- `script/Wa2Paths.cs`
- `script/Wa2SmokeTest.cs`
- `script/Wa2TopMenu.cs`
- `tools/build_macos.sh`
- `tools/extract_windows_icon.py`
- `tools/pak.py`
- `tools/play_muted.sh`
- `tools/prepare_assets.py`
- `tools/prepare_movies.py`
- `tools/test_macos.sh`
- `tools/verify_bundle.py`
- `tools/verify_resources.py`

## Preservation

The original source/LICENSE, source/NOTICE, source/PORTING_CODE.md and source/LICENSES/Apache-2.0.txt
match the pinned upstream blobs after Git text normalization. Original local installers, user game data,
installed apps and save backups were not modified during repository preparation.

## Validation

The local macOS build previously passed CC smoke/save-load, native menu and keyboard tests on
Apple M4/macOS 15.3. Intel hardware, other macOS versions and full routes remain unverified.
This repository preparation changes Git policy/docs and license headers, not game runtime behavior.
