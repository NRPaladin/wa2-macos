// Local macOS port, 2026-10-10. Portable resource discovery; saves never enter the app.
using System;
using System.IO;
using System.Linq;
using Godot;

public static class Wa2Paths
{
    public static readonly string[] RequiredArchives = {
        "BGM.PAK", "bak.pak", "grp.pak", "char.pak", "VOICE.PAK", "SE.PAK", "ck-gal.pak",
        "IC/BGM.PAK", "IC/bak.pak", "IC/grp.pak", "IC/char.pak", "IC/VOICE.PAK", "IC/SE.PAK"
    };
    public static string Argument(string name)
    {
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.Ordinal)) return args[i][(name.Length + 1)..];
            if (args[i] == name && i + 1 < args.Length) return args[i + 1];
        }
        return null;
    }
    public static string Resolve()
    {
        var explicitPath = Argument("--resource-dir") ?? System.Environment.GetEnvironmentVariable("WA2_RESOURCE_DIR");
        if (!string.IsNullOrWhiteSpace(explicitPath)) return Normalize(explicitPath);
        string exe = Path.GetDirectoryName(OS.GetExecutablePath());
        string[] candidates = {
            Path.Combine(exe, "../Resources/Wa2Res"),
            Path.Combine(exe, "../../../Wa2Res"),
            Path.Combine(exe, "Wa2Res"),
            ProjectSettings.GlobalizePath("res://Wa2Res"),
            ProjectSettings.GlobalizePath("res://assets")
        };
        foreach (string candidate in candidates)
            if (File.Exists(Path.Combine(candidate, "ck-gal.pak"))) return Normalize(candidate);
        throw new DirectoryNotFoundException("未找到 Wa2Res。请将 Wa2Res 与 WHITE ALBUM2.app 放在同一文件夹，或使用 --resource-dir 指定资源目录。");
    }
    public static string Normalize(string path) => Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/') + "/";
    public static void Validate(string root)
    {
        foreach (string file in RequiredArchives)
            if (!File.Exists(Path.Combine(root, file))) throw new FileNotFoundException("缺少游戏资源：" + Path.Combine(root, file));
    }
    public static string PrepareTestSaves(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("自动测试必须显式传入 --save-dir，禁止使用现有游戏存档。");
        string dir = Path.GetFullPath(path);
        string marker = Path.Combine(dir, ".wa2-isolated-test");
        if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() && !File.Exists(marker))
            throw new InvalidOperationException("拒绝在非空的非测试目录写入存档：" + dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(marker, "Created exclusively for WA2 integration tests.\n");
        return Normalize(dir);
    }
}
