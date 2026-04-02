using SmartMouth.App.Models;

namespace SmartMouth.App.Services;

public sealed class MouseOffsetEngine : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly MouseController _mouseController;
    private readonly Func<AppConfig> _configAccessor;
    private readonly Timer _timer;

    private bool _isActive;
    private bool _disposed;

    private DateTime _nextUpUtc = DateTime.MinValue;
    private DateTime _nextDownUtc = DateTime.MinValue;
    private DateTime _nextLeftUtc = DateTime.MinValue;
    private DateTime _nextRightUtc = DateTime.MinValue;

    public MouseOffsetEngine(MouseController mouseController, Func<AppConfig> configAccessor)
    {
        _mouseController = mouseController;
        _configAccessor = configAccessor;
        _timer = new Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _isActive;
            }
        }
    }

    public void SetActive(bool active)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _isActive = active;
            if (!active)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            var now = DateTime.UtcNow;
            _nextUpUtc = now;
            _nextDownUtc = now;
            _nextLeftUtc = now;
            _nextRightUtc = now;
            _timer.Change(0, 5);
        }
    }

    private void Tick(object? state)
    {
        AppConfig config;
        lock (_syncRoot)
        {
            if (_disposed || !_isActive)
            {
                return;
            }

            config = _configAccessor();
        }

        var now = DateTime.UtcNow;
        var dx = 0;
        var dy = 0;

        if (config.Up.Enabled && now >= _nextUpUtc)
        {
            dy -= config.Up.OffsetPixels;
            _nextUpUtc = now.AddMilliseconds(config.Up.IntervalMs);
        }

        if (config.Down.Enabled && now >= _nextDownUtc)
        {
            dy += config.Down.OffsetPixels;
            _nextDownUtc = now.AddMilliseconds(config.Down.IntervalMs);
        }

        if (config.Left.Enabled && now >= _nextLeftUtc)
        {
            dx -= config.Left.OffsetPixels;
            _nextLeftUtc = now.AddMilliseconds(config.Left.IntervalMs);
        }

        if (config.Right.Enabled && now >= _nextRightUtc)
        {
            dx += config.Right.OffsetPixels;
            _nextRightUtc = now.AddMilliseconds(config.Right.IntervalMs);
        }

        if (dx != 0 || dy != 0)
        {
            _mouseController.MoveBy(dx, dy);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
        }
    }
}
