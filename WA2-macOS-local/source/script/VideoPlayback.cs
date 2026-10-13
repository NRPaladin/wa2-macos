// Local macOS port: replace platform-specific WMV GDExtension with built-in Theora.
// Original wrapper API retained for the WA2 engine. Modified 2026-10-10.
using System;
using Godot;

[GlobalClass]
public partial class VideoPlayback : Control
{
    [Signal] public delegate void FrameChangedEventHandler(int frameNr);
    [Signal] public delegate void NextFrameCalledEventHandler(int frameNr);
    [Signal] public delegate void VideoLoadedEventHandler();
    [Signal] public delegate void VideoEndedEventHandler();
    [Signal] public delegate void PlaybackStartedEventHandler();
    [Signal] public delegate void PlaybackPausedEventHandler();
    [Signal] public delegate void PlaybackReadyEventHandler();

    private VideoStreamPlayer _player;
    private bool _loaded;
    private int _requestId;
    private string _path = "";
    private bool _enableAudio = true;
    private bool _loop;
    private float _duration;
    private float _fps = 30;

    [Export(PropertyHint.File, "*.ogv")]
    public string Path { get => _path; set => SetVideoPath(value); }
    [Export] public bool EnableAudio { get => _enableAudio; set { _enableAudio = value; if (_player != null) _player.VolumeDb = value ? 0 : -80; } }
    [Export] public bool Loop { get => _loop; set { _loop = value; if (_player != null) _player.Loop = value; } }
    [Export] public bool EnableAutoPlay { get; set; }
    [Export] public bool AudioSpeedToSync { get; set; }
    [Export] public float ForcedFramerate { get; set; }
    [Export] public float PlaybackSpeed { get; set; } = 1;
    [Export] public bool PitchAdjust { get; set; } = true;
    [Export] public bool Debug { get; set; }
    public bool IsPlaying => _loaded && _player != null && _player.IsPlaying() && !_player.Paused;
    public int CurrentFrame { get; private set; }

    public override void _EnterTree()
    {
        _player = new VideoStreamPlayer { Name = "TheoraPlayer", Expand = true,
            MouseFilter = MouseFilterEnum.Ignore, VolumeDb = _enableAudio ? 0 : -80, Loop = _loop };
        AddChild(_player);
        _player.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _player.Finished += () => { if (_loaded) EmitSignal(SignalName.VideoEnded); };
    }
    public override void _Ready()
    {
        EmitSignal(SignalName.PlaybackReady);
        if (EnableAutoPlay && !string.IsNullOrEmpty(_path)) SetVideoPath(_path);
    }
    public override void _Process(double delta)
    {
        if (!IsPlaying) return;
        int frame = Mathf.Max(0, Mathf.FloorToInt(_player.StreamPosition * GetVideoFramerate()));
        if (frame == CurrentFrame) return;
        CurrentFrame = frame;
        EmitSignal(SignalName.FrameChanged, frame);
        EmitSignal(SignalName.NextFrameCalled, frame);
    }
    public async void SetVideoPath(string path)
    {
        Close();
        _path = path;
        int request = ++_requestId;
        // Always defer notification: the caller connects after SetVideoPath returns.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (request != _requestId || !IsInsideTree()) return;
        try
        {
            if (!FileAccess.FileExists(path)) throw new Exception($"Movie missing: {path}");
            _player.Stream = new VideoStreamTheora { File = path };
            _duration = (float)_player.GetStreamLength();
            string metadata = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "movies.json");
            if (System.IO.File.Exists(metadata))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(metadata));
                if (doc.RootElement.TryGetProperty(System.IO.Path.GetFileNameWithoutExtension(path), out var item))
                {
                    _duration = item.GetProperty("duration").GetSingle();
                    _fps = item.GetProperty("fps").GetSingle();
                }
            }
            if (_duration <= 0) throw new Exception($"Movie duration unavailable: {path}");
            _loaded = true;
        }
        catch (Exception ex) { GD.PushError(ex.Message); _loaded = false; }
        EmitSignal(SignalName.VideoLoaded);
    }
    public void Play()
    {
        if (!_loaded) return;
        if (_player.Paused) _player.Paused = false;
        else _player.Play();
        EmitSignal(SignalName.PlaybackStarted);
    }
    public void Pause()
    {
        if (!IsPlaying) return;
        _player.Paused = true;
        EmitSignal(SignalName.PlaybackPaused);
    }
    public void Close()
    {
        ++_requestId;
        _loaded = false;
        CurrentFrame = 0;
        _duration = 0;
        _fps = 30;
        if (_player == null) return;
        _player.Stop();
        _player.Paused = false;
        _player.Stream = null;
    }
    public override void _ExitTree() => Close();
    public bool IsOpen() => _loaded;
    public float GetVideoLength() => _duration;
    public float GetVideoFramerate() => ForcedFramerate > 0 ? ForcedFramerate : _fps;
    public int GetVideoFrameCount() => Mathf.CeilToInt(_duration * GetVideoFramerate());
    public int GetVideoRotation() => 0;
}
