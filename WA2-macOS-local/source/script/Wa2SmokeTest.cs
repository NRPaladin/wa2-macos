// Local macOS handoff: opt-in integration checks using real game data and isolated saves.
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Wa2SmokeTest : Node
{
    private Wa2EngineMain E => Wa2EngineMain.Engine;
    private string _output;
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private async Task Screenshot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_output, name + ".png"));
        Require(error == Error.Ok, "Screenshot failed: " + name);
    }
    private async Task CheckDesktopMenu()
    {
        if (E.TopMenu == null || DisplayServer.GetName() == "headless") return;
        var env = E.TopMenu.GetMenu(1);
        if (OS.GetName() == "macOS")
        {
            Require(E.TopMenu.UsesNativeMenu && env.IsNativeMenu(), "macOS menus are not bound to the native menu bar");
            Require(!E.TopMenu.BarVisible, "In-game menu strip must not appear on macOS");
            // Native menus run the OS tracking loop; actual mouse interaction is verified separately.
            env.EmitSignal(PopupMenu.SignalName.AboutToPopup);
        }
        else
        {
            env.Position = new Vector2I(140, 42);
            env.Popup();
        }
        await Wait(0.3);
        Require(GetTree().Paused && E.TopMenu.IsOpen, "Menu failed to hold game while open");
        if (E.TopMenu.UsesNativeMenu) env.EmitSignal(PopupMenu.SignalName.PopupHide);
        else E.TopMenu.Dismiss();
        await Wait(0.1);
        Require(!GetTree().Paused, "Closing native menu did not resume gameplay");
        var speed = env.GetItemSubmenuNode(5);
        Require(speed != null, "Text speed submenu missing");
        int before = E.Prefs.GetConfig("msg_wait");
        speed.EmitSignal(PopupMenu.SignalName.IdPressed, 4L);
        var saved = new ConfigFile();
        Require(saved.Load(E.SavPath + "SYSTEM.ini") == Error.Ok, "Menu preferences not saved");
        Require(E.Prefs.GetConfig("msg_wait") == 4, "Menu preference did not reach engine");
        Require(saved.GetValue("DEFAULT", "msg_wait").AsInt32() == 4, "Menu preference did not persist");
        E.TopMenu.Dismiss();
        await Wait(0.1);
        Require(!GetTree().Paused && !E.TopMenu.IsOpen, "Menu dismissal left gameplay paused");
        E.Prefs.SetConfig("msg_wait", before);
        E.Prefs.Save();
        env.EmitSignal(PopupMenu.SignalName.IdPressed, 10000L);
        await Wait(0.3);
        Require(E.UiMgr.UiQueue.Peek() == E.UiMgr.OptionsMenu && E.UiMgr.OptionsMenu.PageList.GetChild<Control>(1).Visible, "Sound menu did not open sound settings");
        await Screenshot("top-menu-sound-settings");
        E.UiMgr.ReturnScene();
        GD.Print("SMOKE desktop menus, pause/resume, sound settings, preferences persistence passed");
    }
    private async Task CheckDialogueKeyboard()
    {
        void KeyEvent(Key key, bool pressed = true, bool echo = false) =>
            E._Input(new InputEventKey { Keycode = key, Pressed = pressed, Echo = echo });
        void Press(Key key) { KeyEvent(key); KeyEvent(key, false); }
        uint position = E.Script.ScriptPos;
        string text = E.AdvMain.TextLabel.Text;
        Press(Key.Space);
        Require(!E.AdvMain.Visible && E.AdvMain.State == Wa2AdvMain.AdvState.HIDE, "Space did not hide dialogue UI");
        await Wait(0.3);
        Require(E.Script.ScriptPos == position, "Hiding UI advanced the script");
        KeyEvent(Key.Space, echo: true);
        Require(!E.AdvMain.Visible, "Key repeat toggled UI");
        await Screenshot("keyboard-ui-hidden");
        Press(Key.Space);
        Require(E.AdvMain.Visible && E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK && E.AdvMain.TextLabel.Text == text, "Space did not restore the same dialogue");
        Press(Key.Space);
        Press(Key.Enter);
        Require(E.AdvMain.Visible && E.Script.ScriptPos == position, "Enter must first restore hidden UI without advancing");
        KeyEvent(Key.Enter, echo: true);
        Require(E.Script.ScriptPos == position, "Holding Enter advanced dialogue repeatedly");
        E.UiMgr.OpenOptionsMenu();
        Press(Key.Space); Press(Key.Enter);
        Require(E.Script.ScriptPos == position && E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK, "Dialogue shortcuts leaked into settings");
        E.UiMgr.ReturnScene();
        await Wait(0.4);
        foreach (Key key in new[] { Key.Enter, Key.KpEnter })
        {
            // Test both keys from the same saved line, after its load transition.
            E.GameSav.LoadData(99);
            for (int i = 0; i < 100; i++)
            {
                await Wait(0.1);
                if (E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK && !E.WaitTimer.IsActive() && !E.AnimatorMgr.WaitAnimation()) break;
            }
            Require(!E.WaitTimer.IsActive() && !E.AnimatorMgr.WaitAnimation(), "Dialogue transition is still running");
            position = E.Script.ScriptPos;
            text = E.AdvMain.TextLabel.Text;
            GD.Print($"SMOKE keyboard {key} before script={position} state={E.AdvMain.State} waitKey={E.AdvMain.WaitKey}");
            Press(key);
            for (int i = 0; i < 150; i++)
            {
                await Wait(0.1);
                if (E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK && E.Script.ScriptPos != position) break;
            }
            Require(E.Script.ScriptPos != position && E.AdvMain.TextLabel.Text != text, "Enter did not reach the next dialogue: " + key);
            Require(E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK, "Dialogue did not settle after Enter");
        }
        await Screenshot("keyboard-next-dialogue");
        E.GameSav.LoadData(99);
        GD.Print("SMOKE Enter/keypad Enter advance, Space hide/restore, held-key and settings guards passed");
    }
    private async Task CheckAudio()
    {
        string driver = AudioServer.GetDriverName();
        Require(driver != "Dummy", "Audible test requires a real audio driver");
        GD.Print("AUDIO driver=" + driver + " device=" + AudioServer.OutputDevice);
        async Task CheckBus(int bus, string label)
        {
            float peak = -200;
            for (int i = 0; i < 20; i++)
            {
                await Wait(0.1);
                peak = Math.Max(peak, Math.Max(AudioServer.GetBusPeakVolumeLeftDb(bus, 0), AudioServer.GetBusPeakVolumeRightDb(bus, 0)));
            }
            Require(peak > -65, label + " audio bus has no output");
            GD.Print($"AUDIO {label} peak={peak:F1} dB passed");
        }
        await CheckBus(1, "BGM");
        using var voice = new AudioStreamPlayer { Bus = "VOICE", Stream = Wa2Resource.GetOggStream(Wa2Resource.FileDic.Keys.First(k => k.EndsWith(".ogg") && k.Contains('_') && char.IsDigit(k[0]))) };
        AddChild(voice);
        voice.Play();
        await CheckBus(3, "VOICE");
        voice.Stop();
        RemoveChild(voice);
        E.SoundMgr.PlaySysSe(Wa2Resource.GetSeStream(9015));
        await CheckBus(2, "SE");
        GD.Print("AUDIO real-driver BGM/VOICE/SE output passed (not a listening assessment)");
    }
    public override async void _Ready()
    {
        try
        {
            Require(Wa2Paths.Argument("--save-dir") != null, "Smoke test requires --save-dir (never use personal saves).");
            _output = ProjectSettings.GlobalizePath(E.SavPath);
            GD.Print("SMOKE BEGIN " + OS.GetName() + " " + Engine.GetArchitectureName());
            Require(!ProjectSettings.GetSetting("application/boot_splash/show_image").AsBool(), "Engine boot splash must be disabled");
            var subtitleFont = E.SubtitleMgr.TextLabel.LabelSettings?.Font
                ?? E.SubtitleMgr.TextLabel.GetThemeFont("font");
            var subtitleChars = E.SubtitleMgr.SoundSubtitleList.SelectMany(s => s.Content)
                .Concat(E.SubtitleMgr.VoiceSubtitleList.SelectMany(s => s.Content))
                .SelectMany(s => s.Text).Where(c => !char.IsControl(c)).Distinct();
            foreach (char c in subtitleChars)
                Require(subtitleFont.HasChar(c), $"Subtitle font missing U+{(int)c:X4}");
            GD.Print("SMOKE subtitle glyph coverage passed: " + subtitleFont.GetFontName());
            Require(Wa2Resource.FileDic.Count > 1000, "Archive index unexpectedly small");
            foreach (string script in new[] { "1001", "2001", "3001" })
            {
                var parsed = new Wa2Script(script);
                Require(parsed.Texts.Count > 0 && parsed.Points.Count > 0, "Script cannot load: " + script);
                GD.Print("SMOKE script " + script + " loaded");
            }
            Require(Wa2Resource.GetTgaImage("sys_00100.tga") != null, "Runtime TGA decode failed");
            Require(Wa2Resource.GetBgmStream(1) != null, "BGM decode failed");
            var voice = Wa2Resource.FileDic.Keys.First(k => k.EndsWith(".ogg") && k.Contains('_') && char.IsDigit(k[0]));
            Require(Wa2Resource.GetOggStream(voice) != null, "Voice decode failed");
            // Let the actual logo movie finish and enter the title through its completion signal.
            for (int i = 0; i < 350 && E.State != Wa2EngineMain.GameState.TITLE; i++) await Wait(0.1);
            Require(E.State == Wa2EngineMain.GameState.TITLE, "Logo did not finish into title within 35 seconds");
            await Wait(2);
            await Screenshot("title");
            GD.Print("SMOKE movie completion -> title passed");
            await CheckDesktopMenu();
            if (OS.GetCmdlineUserArgs().Contains("--audio-check")) await CheckAudio();
            E.UiMgr.OpenOptionsMenu();
            await Wait(0.2);
            await Screenshot("options");
            E.UiMgr.ReturnScene();
            E.UiMgr.OpenLoadMenu();
            await Wait(0.2);
            await Screenshot("load-empty");
            E.UiMgr.ReturnScene();
            string chapter = Wa2Paths.Argument("--smoke-chapter") ?? "2001";
            Require(new[] { "1001", "2001", "3001" }.Contains(chapter), "Unsupported smoke chapter");
            E.StartScript(chapter);
            E.UiMgr.OpenGame();
            // Coda starts with a timed radio report: the prior 60s limit cut off
            // an advancing script, not a hung engine. Preserve its original timing.
            int chapterLimit = chapter == "3001" ? 2400 : 1200;
            for (int i = 0; i < chapterLimit; i++)
            {
                await Wait(0.1);
                if (i % 100 == 99)
                {
                    GD.Print($"SMOKE progress chapter={chapter} state={E.State} adv={E.AdvMain.State} ui={E.UiMgr.UiQueue.Peek().Name} script={E.Script.ScriptName}:{E.Script.ScriptPos} video={E.VideoPlayer.IsPlaying} wait={E.WaitTimer.IsActive()} waitType={E.WaitTimer.Type} waitProgress={E.WaitTimer.GetProgress():F3} animation={E.AnimatorMgr.WaitAnimation()}");
                    await Screenshot("progress-" + chapter);
                }
                if (E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK && !E.VideoPlayer.IsPlaying) break;
            }
            Require(E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK, "Chapter did not reach first dialogue: " + chapter);
            Require(!string.IsNullOrEmpty(E.AdvMain.TextLabel.Text), "Empty first dialogue");
            await Screenshot("chapter-" + chapter);
            if (DisplayServer.GetName() != "headless")
            {
                string text = E.AdvMain.TextLabel.Text;
                uint position = E.Script.ScriptPos;
                E.GameSav.SaveData(99);
                E.GameSav.LoadData(99);
                Require(E.Script.ScriptPos == position && E.AdvMain.TextLabel.Text == text, "Save/load round trip changed dialogue");
                await Wait(0.2);
                E.UiMgr.OpenSaveMenu();
                await Wait(0.2);
                await Screenshot("save-menu");
                E.UiMgr.ReturnScene();
                GD.Print("SMOKE save/load round trip passed");
                await Wait(0.4);
                await CheckDialogueKeyboard();
            }
            Require(E.ErrorMessage._errorMessageList.Count == 0, "Game reported an error");
            GD.Print("SMOKE PASS chapter=" + chapter);
            System.IO.File.WriteAllText(System.IO.Path.Combine(_output, "smoke-result.txt"), "PASS " + OS.GetName() + " chapter=" + chapter + "\n");
            E.SysSav?.Close();
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PushError("SMOKE FAIL: " + ex);
            GetTree().Quit(1);
        }
    }
}
