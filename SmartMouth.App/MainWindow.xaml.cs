using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SmartMouth.App.Models;
using SmartMouth.App.Services;

namespace SmartMouth.App;

public enum RuntimeState
{
    Stopped = 0,
    Running = 1,
    Paused = 2,
    Error = 3
}

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly GlobalTriggerListener _triggerListener = new();
    private readonly MouseController _mouseController = new();

    private readonly MouseOffsetEngine _offsetEngine;
    private AppConfig _config;

    private bool _listenerStarted;
    private RuntimeState _runtimeState = RuntimeState.Stopped;

    public MainWindow()
    {
        InitializeComponent();

        _config = _configService.Load();
        _offsetEngine = new MouseOffsetEngine(_mouseController, () => _config.Clone());

        SetupCombos();
        LoadConfigToUi(_config);
        ApplyTriggerSelectionUiState();

        _triggerListener.HoldStateChanged += OnHoldStateChanged;
        _triggerListener.ToggleRequested += OnToggleRequested;

        UpdateStatus(RuntimeState.Stopped, "Stopped");
    }

    private void SetupCombos()
    {
        TriggerTypeCombo.ItemsSource = Enum.GetValues(typeof(TriggerInputType));
        TriggerModeCombo.ItemsSource = Enum.GetValues(typeof(TriggerMode));
        MouseButtonCombo.ItemsSource = Enum.GetValues(typeof(TriggerMouseButton));

        var keys = Enum.GetValues<Key>()
            .Where(k => k is >= Key.F1 and <= Key.F24 || k is >= Key.A and <= Key.Z || k is >= Key.D0 and <= Key.D9)
            .OrderBy(k => k.ToString())
            .ToList();
        KeyboardKeyCombo.ItemsSource = keys;
    }

    private void LoadConfigToUi(AppConfig config)
    {
        TriggerTypeCombo.SelectedItem = config.TriggerInputType;
        TriggerModeCombo.SelectedItem = config.TriggerMode;

        var mappedKey = KeyInterop.KeyFromVirtualKey(config.TriggerVirtualKey);
        if (!KeyboardKeyCombo.Items.Contains(mappedKey))
        {
            mappedKey = Key.F5;
        }

        KeyboardKeyCombo.SelectedItem = mappedKey;
        MouseButtonCombo.SelectedItem = config.TriggerMouseButton;

        BindDirectionToUi(config.Up, UpEnabledCheck, UpOffsetText, UpIntervalText);
        BindDirectionToUi(config.Down, DownEnabledCheck, DownOffsetText, DownIntervalText);
        BindDirectionToUi(config.Left, LeftEnabledCheck, LeftOffsetText, LeftIntervalText);
        BindDirectionToUi(config.Right, RightEnabledCheck, RightOffsetText, RightIntervalText);

        AutoStartCheck.IsChecked = config.AutoStartWithWindows;
        MinimizeToTrayCheck.IsChecked = config.MinimizeToTray;
        AutoRecoverCheck.IsChecked = config.AutoRecoverOnError;
    }

    private AppConfig BuildConfigFromUi()
    {
        var config = _config.Clone();

        config.TriggerInputType = (TriggerInputType)(TriggerTypeCombo.SelectedItem ?? TriggerInputType.Keyboard);
        config.TriggerMode = (TriggerMode)(TriggerModeCombo.SelectedItem ?? TriggerMode.Toggle);
        config.TriggerMouseButton = (TriggerMouseButton)(MouseButtonCombo.SelectedItem ?? TriggerMouseButton.XButton1);

        var selectedKey = (Key)(KeyboardKeyCombo.SelectedItem ?? Key.F5);
        config.TriggerVirtualKey = KeyInterop.VirtualKeyFromKey(selectedKey);

        config.Up = BuildDirectionFromUi(UpEnabledCheck, UpOffsetText, UpIntervalText, config.Up);
        config.Down = BuildDirectionFromUi(DownEnabledCheck, DownOffsetText, DownIntervalText, config.Down);
        config.Left = BuildDirectionFromUi(LeftEnabledCheck, LeftOffsetText, LeftIntervalText, config.Left);
        config.Right = BuildDirectionFromUi(RightEnabledCheck, RightOffsetText, RightIntervalText, config.Right);

        config.AutoStartWithWindows = AutoStartCheck.IsChecked == true;
        config.MinimizeToTray = MinimizeToTrayCheck.IsChecked == true;
        config.AutoRecoverOnError = AutoRecoverCheck.IsChecked != false;

        config.Clamp();
        return config;
    }

    private static void BindDirectionToUi(DirectionSetting setting, System.Windows.Controls.CheckBox enabledCheckBox, System.Windows.Controls.TextBox offsetTextBox, System.Windows.Controls.TextBox intervalTextBox)
    {
        enabledCheckBox.IsChecked = setting.Enabled;
        offsetTextBox.Text = setting.OffsetPixels.ToString();
        intervalTextBox.Text = setting.IntervalMs.ToString();
    }

    private static DirectionSetting BuildDirectionFromUi(
        System.Windows.Controls.CheckBox enabledCheckBox,
        System.Windows.Controls.TextBox offsetTextBox,
        System.Windows.Controls.TextBox intervalTextBox,
        DirectionSetting fallback)
    {
        var parsedOffset = int.TryParse(offsetTextBox.Text, out var offsetValue) ? offsetValue : fallback.OffsetPixels;
        var parsedInterval = int.TryParse(intervalTextBox.Text, out var intervalValue) ? intervalValue : fallback.IntervalMs;

        return new DirectionSetting
        {
            Enabled = enabledCheckBox.IsChecked == true,
            OffsetPixels = parsedOffset,
            IntervalMs = parsedInterval
        };
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _config = BuildConfigFromUi();
            _configService.Save(_config);

            if (_listenerStarted)
            {
                ApplyBindingToListener();
            }

            UpdateStatus(_runtimeState, "Settings saved");
        }
        catch (Exception ex)
        {
            UpdateStatus(RuntimeState.Error, $"Save failed: {ex.Message}");
        }
    }

    private void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _config = BuildConfigFromUi();
            _configService.Save(_config);

            ApplyBindingToListener();
            _triggerListener.Start();
            _listenerStarted = true;

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;

            UpdateStatus(RuntimeState.Paused, "Listener started, waiting for trigger");
        }
        catch (Exception ex)
        {
            UpdateStatus(RuntimeState.Error, $"Start failed: {ex.Message}");
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopListenerAndOffset("Stopped");
    }

    private void ForceStopButton_OnClick(object sender, RoutedEventArgs e)
    {
        _offsetEngine.SetActive(false);
        UpdateStatus(_listenerStarted ? RuntimeState.Paused : RuntimeState.Stopped, "Offset forced to stop");
    }

    private void TriggerTypeCombo_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ApplyTriggerSelectionUiState();
    }

    private void ApplyTriggerSelectionUiState()
    {
        var selectedType = (TriggerInputType)(TriggerTypeCombo.SelectedItem ?? TriggerInputType.Keyboard);
        KeyboardKeyCombo.IsEnabled = selectedType == TriggerInputType.Keyboard;
        MouseButtonCombo.IsEnabled = selectedType == TriggerInputType.Mouse;
    }

    private void ApplyBindingToListener()
    {
        _triggerListener.UpdateBinding(
            _config.TriggerInputType,
            _config.TriggerVirtualKey,
            _config.TriggerMouseButton,
            _config.TriggerMode);
    }

    private void OnHoldStateChanged(bool isDown)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_listenerStarted)
            {
                return;
            }

            if (!_config.HasAnyDirectionEnabled)
            {
                _offsetEngine.SetActive(false);
                UpdateStatus(RuntimeState.Paused, "No direction enabled");
                return;
            }

            _offsetEngine.SetActive(isDown);
            UpdateStatus(isDown ? RuntimeState.Running : RuntimeState.Paused,
                isDown ? "Running (hold active)" : "Paused (hold released)");
        });
    }

    private void OnToggleRequested()
    {
        Dispatcher.Invoke(() =>
        {
            if (!_listenerStarted)
            {
                return;
            }

            if (!_config.HasAnyDirectionEnabled)
            {
                _offsetEngine.SetActive(false);
                UpdateStatus(RuntimeState.Paused, "No direction enabled");
                return;
            }

            var nextState = !_offsetEngine.IsActive;
            _offsetEngine.SetActive(nextState);
            UpdateStatus(nextState ? RuntimeState.Running : RuntimeState.Paused,
                nextState ? "Running (toggle on)" : "Paused (toggle off)");
        });
    }

    private void StopListenerAndOffset(string reason)
    {
        try
        {
            _triggerListener.Stop();
            _listenerStarted = false;
            _offsetEngine.SetActive(false);
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            UpdateStatus(RuntimeState.Stopped, reason);
        }
        catch (Exception ex)
        {
            UpdateStatus(RuntimeState.Error, $"Stop failed: {ex.Message}");
        }
    }

    private void UpdateStatus(RuntimeState state, string details)
    {
        _runtimeState = state;
        StatusText.Text = $"Status: {_runtimeState} | {details}";
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        StopListenerAndOffset("Application closing");
        _triggerListener.Dispose();
        _offsetEngine.Dispose();
        base.OnClosing(e);
    }
}
