# WHITE ALBUM2 macOS 移植源码

基于 [dorakyuraduang/wa2-godot](https://github.com/dorakyuraduang/wa2-godot) 的
`4fc5a89274d9d0dcfcb2e39ead92353ef1422c4c`，包含 macOS 原生菜单、键盘操作、资源加载与本地构建工具。
Godot 工程在 `WA2-macOS-local/source/`。这是用于搭配用户自备游戏数据的源码仓库。

仓库只接纳源码、Godot 场景／着色器／动画配置、资源恢复工具，以及有许可的 Noto 字体和许可证。
Windows 程序、游戏 PAK／脚本、图像、声音、影片、原作字体、混合来源字幕、原版图标、
存档、完整安装包和本机验证记录均在 Git 排除范围内。
本地 `final-installer/` 是包含游戏资源的个人安装包，不是可公开发布的仓库产物。

## 使用自己的游戏文件构建

当前已验证的数据格式为 IC + CC + Coda 汉化整合目录，包含 `fon.pak`、`fnt.pak`、
`ck-gal.pak` 及 `IC/`。其他版本尚未验证；缺少这些文件时准备工具会列出缺项。
请自行准备所需原作／汉化文件及使用权限。

在 macOS 安装 Godot 4.5.1 .NET、.NET SDK 9.0.3xx、Python 3.11+、
支持 libtheora／libvorbis 的 FFmpeg 和对应 ffprobe。随后在仓库根目录运行：

```bash
python3 -m venv .tools/venv
.tools/venv/bin/python -m pip install Pillow numpy pefile
.tools/venv/bin/python tools/prepare_owned_resources.py \
  --game '/path/to/your/game' \
  --exe '/path/to/your/game/WA2_chs.exe' \
  --ffmpeg '/path/to/ffmpeg'
cd WA2-macOS-local/source
export GODOT_BIN='/Applications/Godot_mono.app/Contents/MacOS/Godot'
bash tools/build_macos.sh
```

准备工具只读取指定原作目录与 EXE，在本地工程的 Git 排除路径生成资源与原版图标，
不会运行 Windows EXE。输出目录已存在时停止，不覆盖已有数据。
首次影片转换需要时间和额外磁盘空间。编译前请为 Godot 安装同版本 macOS 导出模板。

混合来源字幕不随源码提供。准备工具在缺少本地字幕时生成空字幕配置，游戏本身的对白仍由用户数据加载。
有权使用的补充字幕可自行放入本地 `assets/sub.yaml`，该文件始终被 Git 排除。

构建后解压 `build/WHITE ALBUM2-macOS-final.zip`，将生成的 `Wa2Res/` 放在 app 旁边，
或使用 `--resource-dir` 指定它。完整含资源应用只供用户本地使用，不应作为仓库 Release 上传。
普通游戏存档在 `~/Library/Application Support/WA2-Godot-Local/`。

## Git 作者和资源检查

本项目以源码快照方式建立，未导入上游 Git 历史。来源和本地修改记录在 `NOTICE`、`CHANGES.md`。
远程仓库为 `NRPaladin/wa2-macos`，保持 Private；首批源码须经过维护者确认后才推送。
本地 Codex 复核快照不会作为 Git 分支／标签上传。

仓库级身份为 `NRPaladin` / `171237289+NRPaladin@users.noreply.github.com`，全局 Git 设置不变。
根目录 `.gitignore` 使用明确的组件范围；`tools/verify_repository.py` 会继续检查文件路径、
暂存 blob、私钥和个人机器路径。这个检查不代替文件许可证和版权来源核对。

```bash
python3 tools/verify_repository.py --check-identity
```

本机已通过仓库级 `core.hooksPath=.githooks` 启用提交前检查。其他用户克隆后可在仓库内运行：

```bash
git config --local core.hooksPath .githooks
```

提交前检查会拒绝超出源码组件范围的暂存内容，包括通过 `git add -f` 强制加入的游戏资源。
身份检查目前针对本仓库维护者；其他贡献者应使用自己的开发者昵称和 noreply 邮箱，
并调整检查器中的身份配置。

## 许可证与来源

本地新增工具采用 MIT；上游实现保留 Apache-2.0；Noto 字体保留 SIL OFL。
具体范围见根目录 [LICENSE](LICENSE)、[NOTICE](NOTICE) 和
[第三方声明](THIRD_PARTY_NOTICES.md)。

保留上游的 [许可证边界](WA2-macOS-local/source/LICENSE)、
[移植代码范围](WA2-macOS-local/source/PORTING_CODE.md)、
[来源声明](WA2-macOS-local/source/NOTICE) 和
[第三方声明](WA2-macOS-local/source/THIRD_PARTY_NOTICES.md)。
Noto 字体附带 SIL OFL。原作及汉化资源、图标和字幕没有获得本仓库代码许可证的授权。
