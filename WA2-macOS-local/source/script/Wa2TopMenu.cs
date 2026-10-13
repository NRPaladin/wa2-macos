// Native macOS menus, with a hover bar fallback on other desktop platforms.
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Wa2TopMenu : CanvasLayer
{
    private Wa2EngineMain E => Wa2EngineMain.Engine;
    private PanelContainer _bar;
    private HBoxContainer _row;
    private Theme _theme;
    private MenuBar _nativeBar;
    private readonly HashSet<PopupMenu> _openPopups = new();
    public bool UsesNativeMenu => _nativeBar?.IsNativeMenu() == true;
    public PopupMenu GetMenu(int index) => _nativeBar != null ? _nativeBar.GetMenuPopup(index) : _row.GetChild<MenuButton>(index).GetPopup();
    private readonly List<PopupMenu> _popups = new();
    private double _leaveTime;
    private bool _pointerInside;
    private Vector2 _pointer = new(-1, -1);
    private bool _ownsPause;
    public bool IsOpen => _openPopups.Count > 0 || _popups.Any(p => p.Visible);
    public bool BarVisible => _bar?.Visible == true;

    public override void _Ready()
    {
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;
        if (OS.GetName() == "macOS")
        {
            // Godot inserts global MenuBar items before the system Window/Help menus.
            _nativeBar = new MenuBar { Name = "NativeMenuBar", PreferGlobalMenu = true, StartIndex = 0 };
            AddChild(_nativeBar);
        }
        else
        {
        _theme = new Theme { DefaultFont = GD.Load<Font>("res://assets/fonts/unicode/NotoSansCJKsc-Regular.otf"), DefaultFontSize = 19 };
        var paper = new StyleBoxFlat { BgColor = new Color("f3f3f1"), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 5, ContentMarginBottom = 5 };
        var blue = new StyleBoxFlat { BgColor = new Color("3979c6"), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 5, ContentMarginBottom = 5 };
        _theme.SetStylebox("panel", "PanelContainer", paper);
        _theme.SetStylebox("panel", "PopupMenu", paper);
        _theme.SetStylebox("hover", "PopupMenu", blue);
        _theme.SetConstant("v_separation", "PopupMenu", 10);
        Texture2D Icon(string shape) {
            var image = new Image();
            image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='16' height='16'>" + shape + "</svg>");
            return ImageTexture.CreateFromImage(image);
        }
        _theme.SetIcon("submenu", "PopupMenu", Icon("<path d='M6 3l5 5-5 5' fill='none' stroke='#444' stroke-width='2'/>") );
        _theme.SetIcon("radio_checked", "PopupMenu", Icon("<circle cx='8' cy='8' r='5' fill='#3979c6'/>") );
        _theme.SetIcon("radio_unchecked", "PopupMenu", Icon("<circle cx='8' cy='8' r='5' fill='none' stroke='#888'/>") );
        foreach (string type in new[] { "MenuButton", "PopupMenu" })
        {
            _theme.SetColor("font_color", type, new Color("222222"));
            _theme.SetColor("font_focus_color", type, new Color("222222"));
            _theme.SetColor("font_hover_color", type, Colors.White);
            _theme.SetColor("font_pressed_color", type, Colors.White);
            _theme.SetColor("font_disabled_color", type, new Color("999999"));
        }
        foreach (string state in new[] { "normal", "disabled" }) _theme.SetStylebox(state, "MenuButton", paper);
        foreach (string state in new[] { "hover", "pressed", "hover_pressed" }) _theme.SetStylebox(state, "MenuButton", blue);
        _bar = new PanelContainer { Name = "TopMenuBar", Theme = _theme, MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(_bar);
        _bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _row = new HBoxContainer();
        _row.AddThemeConstantOverride("separation", 0);
        _bar.AddChild(_row);
        }
        var screen = Menu("画面设置");
        screen.AddRadioCheckItem("全屏模式    F11 / ⌃⌘F", 0);
        screen.IdPressed += id => { if (id == 0) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen); };
        screen.AboutToPopup += () => screen.SetItemChecked(0, IsFullscreen());
        var sizes = Submenu(screen, "窗口模式");
        foreach (int width in new[] { 960, 1280, 1600, 1920 }) sizes.AddRadioCheckItem($"{width} × {width * 9 / 16}", width);
        sizes.IdPressed += id => SetWindowSize((int)id);
        sizes.AboutToPopup += () => {
            for (int i = 0; i < sizes.ItemCount; i++) sizes.SetItemChecked(i, !IsFullscreen() && DisplayServer.WindowGetSize().X == sizes.GetItemId(i));
        };
        var env = Menu("环境设置");
        env.AddItem("音声管理", 10000);
        Choices(env, "翻页时停止音声", "page_voice", ("停止", 1), ("继续播放", 0));
        Choices(env, "H场景语音", "ero_voice", ("全部角色", 0), ("仅女主角", 1));
        env.AddSeparator();
        env.AddItem("文本窗口设置", 20000);
        Choices(env, "文章显示速度", "msg_wait", ("瞬间", 0), ("高速", 4), ("普通", 2), ("流畅", 1));
        Choices(env, "文本快进", "msg_cut_optin", ("仅已读文本", 0), ("全部文本", 1));
        Choices(env, "长按快进", "checkskip", ("开启", 1), ("关闭", 0));
        env.AddSeparator();
        Choices(env, "显示确认窗口", "yes_no", ("显示", 1), ("不显示", 0));
        env.AddItem("全部环境设置…", 30000);
        env.AboutToPopup += () => {
            bool canOpen = CanUse && (E.UiMgr.UiQueue.Peek() == E.UiMgr.TitleMenu || E.UiMgr.UiQueue.Peek() == E.AdvMain || E.UiMgr.UiQueue.Peek() == E.UiMgr.OptionsMenu);
            foreach (int id in new[] { 10000, 20000, 30000 }) env.SetItemDisabled(env.GetItemIndex(id), !canOpen);
        };
        env.IdPressed += id => OpenOptions(id == 10000 ? 1 : 0);
        var system = Menu("系统");
        system.AddItem("返回标题画面", 1);
        system.AddSeparator();
        system.AddItem("打开存档文件夹", 2);
        system.AddSeparator();
        system.AddItem("结束", 3);
        system.AboutToPopup += () => system.SetItemDisabled(0, !CanReturnTitle());
        system.IdPressed += id => {
            if (id == 1 && CanReturnTitle()) Confirm("返回标题画面？\n尚未保存的进度将丢失。", () => E.UiMgr.OpenTitleMenu());
            if (id == 2) OS.ShellOpen(ProjectSettings.GlobalizePath(E.SavPath));
            if (id == 3) Confirm("结束游戏？\n尚未保存的进度将丢失。", () => { E.Prefs.Save(); E.SysSav?.Flush(); GetTree().Quit(); });
        };
        _bar?.Hide();
        GetWindow().MouseExited += () => _pointerInside = false;
    }
    private bool CanUse => (E.State == Wa2EngineMain.GameState.TITLE || E.State == Wa2EngineMain.GameState.GAME)
        && E.UiMgr.UiQueue.Count > 0 && E.UiMgr.UiQueue.Peek() != E.UiMgr.UIConfirm;
    private bool CanReturnTitle() => E.State == Wa2EngineMain.GameState.GAME
        && E.UiMgr.UiQueue.Count > 0 && E.UiMgr.UiQueue.Peek() == E.AdvMain && E.AdvMain.State == Wa2AdvMain.AdvState.WAIT_CLICK
        && !E.AnimatorMgr.WaitAnimation() && !E.VideoPlayer.IsPlaying;
    private static bool IsFullscreen() => DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
    private PopupMenu Menu(string text)
    {
        if (_nativeBar != null)
        {
            var nativePopup = new PopupMenu { Name = text, Title = text };
            _nativeBar.AddChild(nativePopup);
            Configure(nativePopup);
            return nativePopup;
        }
        var button = new MenuButton { Text = text, Flat = false, SwitchOnHover = true, FocusMode = Control.FocusModeEnum.All };
        _row.AddChild(button);
        var popup = button.GetPopup();
        Configure(popup);
        return popup;
    }
    private void Configure(PopupMenu popup)
    {
        popup.Theme = _theme;
        popup.PreferNativeMenu = _nativeBar != null;
        _popups.Add(popup);
        popup.PopupHide += () => { _pointerInside = false; _openPopups.Remove(popup); };
        popup.AboutToPopup += () => {
            _openPopups.Add(popup);
            _bar?.Show();
            E.IsPressed = false;
            E.PressedTime = 0;
            E.StopSkip();
            if (!GetTree().Paused) { _ownsPause = true; GetTree().Paused = true; }
        };
    }
    private PopupMenu Submenu(PopupMenu parent, string text)
    {
        var child = new PopupMenu { Name = "Sub" + _popups.Count };
        parent.AddChild(child);
        Configure(child);
        parent.AddSubmenuNodeItem(text, child);
        return child;
    }
    private void Choices(PopupMenu parent, string title, string key, params (string Text, int Value)[] choices)
    {
        var menu = Submenu(parent, title);
        foreach (var choice in choices) menu.AddRadioCheckItem(choice.Text, choice.Value);
        menu.AboutToPopup += () => {
            for (int i = 0; i < menu.ItemCount; i++) menu.SetItemChecked(i, E.Prefs.GetConfig(key) == menu.GetItemId(i));
        };
        menu.IdPressed += value => {
            E.Prefs.SetConfig(key, (int)value);
            E.Prefs.Save();
            if (E.UiMgr.OptionsMenu.Visible) E.UiMgr.OptionsMenu.UpdateOption();
        };
    }
    private void OpenOptions(int page)
    {
        Dismiss();
        if (E.UiMgr.UiQueue.Peek() != E.UiMgr.TitleMenu && E.UiMgr.UiQueue.Peek() != E.AdvMain && E.UiMgr.UiQueue.Peek() != E.UiMgr.OptionsMenu) return;
        if (E.UiMgr.UiQueue.Peek() != E.UiMgr.OptionsMenu) E.UiMgr.OpenOptionsMenu();
        E.UiMgr.OptionsMenu.OptionButtonList.GetChild<Wa2Button>(page).ButtonPressed = true;
        E.UiMgr.OptionsMenu.UpdatePage(page);
    }
    private void Confirm(string message, Action action)
    {
        Dismiss();
        E.UiMgr.OpenConfirm(message, "", true, () => { E.UiMgr.UIConfirm.Close(); action(); });
    }
    private void SetWindowSize(int width)
    {
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        var usable = DisplayServer.ScreenGetUsableRect();
        width = Math.Min(width, Math.Min(usable.Size.X, (usable.Size.Y - 70) * 16 / 9));
        var size = new Vector2I(width, width * 9 / 16);
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
    }
    public void Dismiss()
    {
        foreach (var popup in _popups.AsEnumerable().Reverse()) popup.Hide();
        if (_ownsPause) { GetTree().Paused = false; _ownsPause = false; }
        _openPopups.Clear();
        _bar?.Hide();
        _leaveTime = 0;
    }
    public override void _Input(InputEvent input)
    {
        if (_nativeBar != null) return;
        if (input is InputEventMouse mouse)
        {
            _pointer = mouse.Position;
            _pointerInside = true;
            if (CanUse && _pointer.Y <= 12) _bar.Show();
        }
    }
    public override void _Process(double delta)
    {
        if (_ownsPause && !IsOpen) { GetTree().Paused = false; _ownsPause = false; }
        if (_nativeBar != null)
        {
            for (int i = 0; i < _nativeBar.GetMenuCount(); i++) _nativeBar.SetMenuDisabled(i, !CanUse);
            return;
        }
        if (!CanUse) { Dismiss(); return; }
        Vector2 mouse = _pointer;
        bool inside = _pointerInside && mouse.X >= 0 && mouse.X <= GetViewport().GetVisibleRect().Size.X;
        if (IsOpen || (inside && GetWindow().HasFocus() && mouse.Y <= (_bar.Visible ? _bar.Size.Y : 12)))
        { _bar.Show(); _leaveTime = 0; }
        else if ((_leaveTime += delta) >= 0.35) _bar.Hide();
    }
    public override void _ExitTree() { if (_ownsPause) GetTree().Paused = false; }
}
