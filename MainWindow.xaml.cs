using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using OwTranslateLite.Core;
using OwTranslateLite.Ocr;
using OwTranslateLite.Overlay;
using OwTranslateLite.Translation;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace OwTranslateLite;

public partial class MainWindow : Window
{
    private static readonly TimeSpan OverlayIdleHideDelay = TimeSpan.FromSeconds(6);
    private const int MaxOverlayRecords = 50;
    private const int MaxLogRecords = 200;

    private readonly ConfigStore _config = new();
    private OwGlossaryService _glossary = null!;
    private TranslationCoordinator _coordinator = null!;
    private OverlayWindow? _overlay;
    private CancellationTokenSource? _loopCts;
    private IOcrEngine? _currentOcrEngine;
    private string? _currentOcrEngineName;
    private string? _currentOcrLanguage;
    private string? _activeRunSettingsKey;
    private DateTime? _pausedAt;
    private DateTime? _lastTranslationCompletedAt;
    private bool _overlayHiddenByIdle;
    private bool _isRunning;
    private bool _isLoadingSettings;
    private readonly List<TranslationRecord> _records = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _config.Load();
        _glossary = OwGlossaryService.LoadDefault();
        _coordinator = new TranslationCoordinator(_config.Settings, _glossary);
        GlossaryStatusText.Text = $"术语 { _glossary.EntryCount } 项 · { _glossary.Version }";
        LoadSettingsToUi();
        EnsureOverlay();
        ApplyRunningState();
        AddLog("就绪。正式测试建议使用 DeepSeek API；Local Rules 仅用于离线规则冒烟测试。");
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopLoop(hideOverlay: false, clearOverlay: false);
        SaveSettingsFromUi();
        _overlay?.Close();
    }

    private void LoadSettingsToUi()
    {
        _isLoadingSettings = true;
        try
        {
            AppSettings settings = _config.Settings;
            SelectCombo(OcrEngineCombo, settings.OcrEngine);
            SelectCombo(OcrLanguageCombo, settings.OcrLanguage);
            SelectCombo(ProviderCombo, settings.TranslationProvider);
            EnsureDefaultModelOptions();
            ApiUrlBox.Text = settings.ApiUrl;
            ApiKeyBox.Password = settings.ApiKey;
            ModelCombo.Text = settings.Model;
            FontSizeSlider.Value = settings.OverlayFontSize;
            OpacitySlider.Value = settings.OverlayOpacity;
            ClickThroughCheck.IsChecked = settings.OverlayClickThrough;
            FirstRunPanel.Visibility = settings.FirstRun ? Visibility.Visible : Visibility.Collapsed;
            UpdateProviderPreset();
            UpdateRegionText();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void SaveSettingsFromUi()
    {
        AppSettings settings = _config.Settings;
        settings.OcrEngine = GetComboText(OcrEngineCombo);
        settings.OcrLanguage = GetComboText(OcrLanguageCombo);
        settings.TranslationProvider = GetComboText(ProviderCombo);
        settings.ApiUrl = ApiUrlBox.Text.Trim();
        settings.ApiKey = ApiKeyBox.Password.Trim();
        settings.Model = ModelCombo.Text.Trim();
        settings.OverlayFontSize = FontSizeSlider.Value;
        settings.OverlayOpacity = OpacitySlider.Value;
        settings.OverlayClickThrough = ClickThroughCheck.IsChecked == true;
        SaveOverlayBounds(settings);
        _config.Save();
        _overlay?.ApplySettings(settings);
    }

    private void SelectCombo(WpfComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static string GetComboText(WpfComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
    }

    private void UpdateProviderPreset()
    {
        string provider = GetComboText(ProviderCombo);
        bool apiEnabled = provider != "Local" && provider != "Local Rules";
        ApiUrlBox.IsEnabled = apiEnabled;
        ApiKeyBox.IsEnabled = apiEnabled;
        ModelCombo.IsEnabled = apiEnabled;
        FetchModelsButton.IsEnabled = apiEnabled;

        if (provider == "DeepSeek")
        {
            EnsureDefaultModelOptions();
            if (string.IsNullOrWhiteSpace(ApiUrlBox.Text) ||
                string.Equals(ApiUrlBox.Text.Trim(), "https://api.deepseek.com/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                ApiUrlBox.Text = "https://api.deepseek.com";
            }

            string model = ModelCombo.Text.Trim();
            if (string.IsNullOrWhiteSpace(model) ||
                string.Equals(model, "deepseek-chat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model, "deepseek-reasoner", StringComparison.OrdinalIgnoreCase))
            {
                ModelCombo.Text = "deepseek-v4-flash";
            }
        }
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isLoadingSettings)
        {
            return;
        }

        UpdateProviderPreset();
        if (_isRunning)
        {
            SaveSettingsFromUi();
            RestartLoop(resetChatCycle: false, resetOcrEngine: false, "翻译设置已更新，已继续运行。");
        }
    }

    private void OcrSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isLoadingSettings)
        {
            return;
        }

        SaveSettingsFromUi();
        _coordinator.ResetChatCycle();
        InvalidateOcrEngine();

        if (_isRunning)
        {
            RestartLoop(resetChatCycle: true, resetOcrEngine: true, "OCR 设置已更新，已重启识别。");
        }
        else
        {
            _activeRunSettingsKey = null;
            AddLog("OCR 设置已更新，下次开始时生效。");
        }
    }

    private void FinishFirstRun_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        _config.Settings.FirstRun = false;
        _config.Save();
        FirstRunPanel.Visibility = Visibility.Collapsed;
        AddLog("首次配置完成。");
    }

    private void SelectArea_Click(object sender, RoutedEventArgs e)
    {
        AreaSelectorWindow selector = new();
        selector.Owner = this;
        selector.SelectionCompleted += (_, rect) =>
        {
            _config.Settings.CaptureRegion = CaptureRegion.FromRect(rect);
            _config.Save();
            UpdateRegionText();
            EnsureOverlay();
            _overlay?.MoveNear(rect);
            AddLog($"已选择区域 {rect.Left:0},{rect.Top:0} {rect.Width:0}x{rect.Height:0}");
        };
        selector.ShowDialog();
    }

    private void ShowOverlay_Click(object sender, RoutedEventArgs e)
    {
        EnsureOverlay();
        _overlay?.Show();
        _overlay?.Activate();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        if (_config.Settings.CaptureRegion is null)
        {
            AddLog("请先选择聊天区域。");
            return;
        }

        EnsureOverlay();
        string settingsKey = CreateRunSettingsKey();
        bool settingsChanged = !string.Equals(_activeRunSettingsKey, settingsKey, StringComparison.Ordinal);
        bool pausedLongEnoughToReset = _pausedAt is DateTime pausedAt && DateTime.Now - pausedAt >= TimeSpan.FromSeconds(3);
        bool resetChatCycle = settingsChanged || pausedLongEnoughToReset;
        RestartLoop(resetChatCycle, settingsChanged, resetChatCycle ? "已开始新的识别会话。" : "已继续运行。");
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopLoop(hideOverlay: true, clearOverlay: false);
        StatusText.Text = "已暂停";
        AddLog("已暂停。");
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        AddLog("设置已保存。");
    }

    private async void FetchModels_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
        if (GetComboText(ProviderCombo) is "Local" or "Local Rules")
        {
            AddLog("Local Rules 是测试模式，不需要获取模型。");
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.Settings.ApiUrl))
        {
            AddLog("请先填写 API URL。");
            return;
        }

        if (string.IsNullOrWhiteSpace(_config.Settings.ApiKey))
        {
            AddLog("请先填写 API Key。");
            return;
        }

        FetchModelsButton.IsEnabled = false;
        try
        {
            AddLog("正在获取模型列表...");
            IReadOnlyList<string> models = await OpenAICompatibleTranslationProvider.FetchModelIdsAsync(
                _config.Settings,
                CancellationToken.None);

            if (models.Count == 0)
            {
                AddLog("没有从 API 返回可用模型。");
                return;
            }

            string current = ModelCombo.Text.Trim();
            ModelCombo.Items.Clear();
            foreach (string model in models)
            {
                AddModelOption(model);
            }

            ModelCombo.Text = models.Contains(current, StringComparer.OrdinalIgnoreCase)
                ? current
                : models[0];
            SaveSettingsFromUi();
            AddLog($"已获取 {models.Count} 个模型。");
        }
        catch (Exception ex)
        {
            AddLog($"获取模型失败：{ex.Message}");
        }
        finally
        {
            FetchModelsButton.IsEnabled = true;
        }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogList.Items.Clear();
        ClearOverlayRecords();
    }

    private void SaveOverlayBounds(AppSettings settings)
    {
        if (_overlay is null)
        {
            return;
        }

        settings.OverlayLeft = _overlay.Left;
        settings.OverlayTop = _overlay.Top;
        settings.OverlayWidth = _overlay.Width;
        settings.OverlayHeight = _overlay.Height;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                IOcrEngine engine = GetOcrEngine();
                IReadOnlyList<TranslationRecord> records = await _coordinator.ProcessAsync(engine, cancellationToken);
                if (records.Count > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        foreach (TranslationRecord record in records)
                        {
                            _records.Add(record);
                            AddLog($"{record.Speaker}: {record.SourceText}  =>  {record.TranslatedText}");
                        }

                        TrimOverlayRecords();
                        _lastTranslationCompletedAt = DateTime.Now;
                        _overlayHiddenByIdle = false;
                        EnsureOverlay();
                        _overlay?.Show();
                        _overlay?.UpdateRecords(_records);
                    });
                }
                else if (_coordinator.ChatCycleJustReset)
                {
                    Dispatcher.Invoke(MaybeHideOverlayAfterIdle);
                }

                stopwatch.Stop();
                Dispatcher.Invoke(() =>
                {
                    MaybeHideOverlayAfterIdle();
                    LatencyText.Text = $"{stopwatch.ElapsedMilliseconds} ms";
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AddLog($"错误：{ex.Message}"));
            }

            int delay = Math.Clamp(_config.Settings.CaptureIntervalMs, 400, 3000);
            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private IOcrEngine GetOcrEngine()
    {
        if (_currentOcrEngine is not null &&
            _currentOcrEngineName == _config.Settings.OcrEngine &&
            _currentOcrLanguage == _config.Settings.OcrLanguage)
        {
            return _currentOcrEngine;
        }

        _currentOcrEngineName = _config.Settings.OcrEngine;
        _currentOcrLanguage = _config.Settings.OcrLanguage;
        _currentOcrEngine = _config.Settings.OcrEngine == "Windows OCR"
            ? new WindowsOcrEngine()
            : new OneOcrEngine();

        return _currentOcrEngine;
    }

    private void RestartLoop(bool resetChatCycle, bool resetOcrEngine, string message)
    {
        StopLoop(hideOverlay: false, clearOverlay: false);

        if (_config.Settings.CaptureRegion is null)
        {
            StatusText.Text = "未选择区域";
            AddLog("请先选择聊天区域。");
            return;
        }

        if (resetChatCycle)
        {
            _coordinator.ResetChatCycle();
        }

        if (resetOcrEngine)
        {
            InvalidateOcrEngine();
        }

        EnsureOverlay();
        _loopCts = new CancellationTokenSource();
        _isRunning = true;
        _pausedAt = null;
        _overlayHiddenByIdle = false;
        _activeRunSettingsKey = CreateRunSettingsKey();
        StatusText.Text = "运行中";
        ApplyRunningState();
        AddLog(message);
        _ = RunLoopAsync(_loopCts.Token);
    }

    private void StopLoop(bool hideOverlay, bool clearOverlay)
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
        _isRunning = false;
        ApplyRunningState();

        if (clearOverlay)
        {
            ClearOverlayRecords();
        }

        if (hideOverlay)
        {
            _pausedAt = DateTime.Now;
            _overlayHiddenByIdle = false;
            _overlay?.Hide();
        }
    }

    private void EnsureOverlay()
    {
        if (_overlay is not null)
        {
            _overlay.ApplySettings(_config.Settings);
            return;
        }

        _overlay = new OverlayWindow();
        _overlay.ApplySettings(_config.Settings);
        if (_config.Settings.CaptureRegion is CaptureRegion region)
        {
            _overlay.MoveNear(region.ToRect());
        }
    }

    private void UpdateRegionText()
    {
        RegionText.Text = _config.Settings.CaptureRegion is CaptureRegion region
            ? $"区域：{region.Left:0},{region.Top:0}  {region.Width:0}x{region.Height:0}"
            : "未选择区域";
    }

    private void AddLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LogList.Items.Add(line);
        while (LogList.Items.Count > MaxLogRecords)
        {
            LogList.Items.RemoveAt(0);
        }

        LogList.ScrollIntoView(line);
    }

    private void EnsureDefaultModelOptions()
    {
        AddModelOption("deepseek-v4-flash");
        AddModelOption("deepseek-v4-pro");
    }

    private void AddModelOption(string model)
    {
        foreach (object? item in ModelCombo.Items)
        {
            if (string.Equals(item?.ToString(), model, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        ModelCombo.Items.Add(model);
    }

    private void ClearOverlayRecords()
    {
        _records.Clear();
        _lastTranslationCompletedAt = null;
        _overlayHiddenByIdle = false;
        _overlay?.UpdateRecords(_records);
    }

    private void TrimOverlayRecords()
    {
        while (_records.Count > MaxOverlayRecords)
        {
            _records.RemoveAt(0);
        }
    }

    private void MaybeHideOverlayAfterIdle()
    {
        if (!_isRunning || _overlay is null || _overlayHiddenByIdle || _coordinator.HasVisibleChat)
        {
            return;
        }

        if (_lastTranslationCompletedAt is not DateTime completedAt ||
            DateTime.Now - completedAt < OverlayIdleHideDelay)
        {
            return;
        }

        _overlay.Hide();
        _overlayHiddenByIdle = true;
    }

    private void InvalidateOcrEngine()
    {
        _currentOcrEngine = null;
        _currentOcrEngineName = null;
        _currentOcrLanguage = null;
    }

    private void ApplyRunningState()
    {
        if (StartButton is null || StopButton is null)
        {
            return;
        }

        StartButton.IsEnabled = !_isRunning;
        StopButton.IsEnabled = _isRunning;
    }

    private string CreateRunSettingsKey()
    {
        CaptureRegion? region = _config.Settings.CaptureRegion;
        string regionKey = region is null
            ? "none"
            : $"{region.Left:0.##},{region.Top:0.##},{region.Width:0.##},{region.Height:0.##}";
        return string.Join("|",
            _config.Settings.OcrEngine,
            _config.Settings.OcrLanguage,
            _config.Settings.TranslationProvider,
            _config.Settings.ApiUrl,
            _config.Settings.Model,
            regionKey);
    }
}
