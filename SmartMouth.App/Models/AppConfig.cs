using System.Text.Json.Serialization;

namespace SmartMouth.App.Models;

public enum TriggerMode
{
    Hold = 0,
    Toggle = 1
}

public enum TriggerInputType
{
    Keyboard = 0,
    Mouse = 1
}

public enum TriggerMouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
    XButton1 = 3,
    XButton2 = 4
}

public sealed class DirectionSetting
{
    public bool Enabled { get; set; }
    public int OffsetPixels { get; set; } = 5;
    public int IntervalMs { get; set; } = 20;

    public DirectionSetting Clone()
    {
        return new DirectionSetting
        {
            Enabled = Enabled,
            OffsetPixels = OffsetPixels,
            IntervalMs = IntervalMs
        };
    }
}

public sealed class AppConfig
{
    public const int MinIntervalMs = 1;
    public const int MaxIntervalMs = 10_000;
    public const int MinOffsetPixels = 1;
    public const int MaxOffsetPixels = 1_000;

    public TriggerInputType TriggerInputType { get; set; } = TriggerInputType.Keyboard;
    public int TriggerVirtualKey { get; set; } = 0x74; // F5
    public TriggerMouseButton TriggerMouseButton { get; set; } = TriggerMouseButton.XButton1;
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Toggle;

    public DirectionSetting Up { get; set; } = new();
    public DirectionSetting Down { get; set; } = new();
    public DirectionSetting Left { get; set; } = new();
    public DirectionSetting Right { get; set; } = new();

    public bool AutoStartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool AutoRecoverOnError { get; set; } = true;

    [JsonIgnore]
    public bool HasAnyDirectionEnabled => Up.Enabled || Down.Enabled || Left.Enabled || Right.Enabled;

    public void Clamp()
    {
        TriggerVirtualKey = Math.Clamp(TriggerVirtualKey, 1, 255);

        ClampDirection(Up);
        ClampDirection(Down);
        ClampDirection(Left);
        ClampDirection(Right);
    }

    public AppConfig Clone()
    {
        return new AppConfig
        {
            TriggerInputType = TriggerInputType,
            TriggerVirtualKey = TriggerVirtualKey,
            TriggerMouseButton = TriggerMouseButton,
            TriggerMode = TriggerMode,
            Up = Up.Clone(),
            Down = Down.Clone(),
            Left = Left.Clone(),
            Right = Right.Clone(),
            AutoStartWithWindows = AutoStartWithWindows,
            MinimizeToTray = MinimizeToTray,
            AutoRecoverOnError = AutoRecoverOnError
        };
    }

    private static void ClampDirection(DirectionSetting setting)
    {
        setting.OffsetPixels = Math.Clamp(Math.Abs(setting.OffsetPixels), MinOffsetPixels, MaxOffsetPixels);
        setting.IntervalMs = Math.Clamp(setting.IntervalMs, MinIntervalMs, MaxIntervalMs);
    }
}
