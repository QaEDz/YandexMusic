using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MuSync.Models;
using MuSync.Utils;
namespace MuSync;
/// <summary>
/// 主界面：三个播放器卡片（歌名/歌手/专辑/进度条/封面）+ Steam 状态。
/// 差量刷新与封面管线逻辑自 MainForm 逐行移植，后台数据源仍为 RpcManager.GetAllPlayersStatus。
/// </summary>
public partial class DashboardPage : Page
{
    private const int PlayerCount = 4;
    private static readonly SolidColorBrush NetEaseBrush = Freeze(Color.FromRgb(241, 98, 70));
    private static readonly SolidColorBrush TencentBrush = Freeze(Color.FromRgb(217, 215, 23));
    private static readonly SolidColorBrush SodaBrush = Freeze(Color.FromRgb(254, 44, 85));
    private static readonly SolidColorBrush YandexBrush = Freeze(Color.FromRgb(255, 204, 0));
    private static readonly SolidColorBrush[] PlayerBrushes = [NetEaseBrush, TencentBrush, SodaBrush, YandexBrush];
    private static readonly string[] PlayerNames = ["NetEase Music", "QQ Music", "Soda Music", "Яндекс Музыка"];

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _perfTimer;
    private readonly TextBlock[] _playerNameLabels = new TextBlock[PlayerCount];
    private readonly Border[] _coverBorders = new Border[PlayerCount];
    private readonly UIElement[] _coverPlaceholders = new UIElement[PlayerCount];
    private readonly TextBlock[] _songTitleLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _artistLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _albumLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _statusLabels = new TextBlock[PlayerCount];
    private readonly ProgressBar[] _progressBars = new ProgressBar[PlayerCount];
    private readonly TextBlock[] _progressLabels = new TextBlock[PlayerCount];
    private readonly string[] _currentSongIds = new string[PlayerCount];
    private readonly string[] _currentCoverUrls = ["", "", "", ""];
    private readonly string[] _currentCacheKeys = ["", "", "", ""];
    private readonly PlayerInfo?[] _lastPlayerInfos = new PlayerInfo?[PlayerCount];
    private readonly bool[] _lastActiveStates = new bool[PlayerCount];

    public DashboardPage()
    {
        InitializeComponent();
        for (var i = 0; i < PlayerCount; i++)
        {
            _playerNameLabels[i] = (TextBlock)FindName($"PlayerName{i}")!;
            _coverBorders[i] = (Border)FindName($"CoverBorder{i}")!;
            _coverPlaceholders[i] = (UIElement)FindName($"CoverPlaceholder{i}")!;
            _songTitleLabels[i] = (TextBlock)FindName($"SongTitle{i}")!;
            _artistLabels[i] = (TextBlock)FindName($"Artist{i}")!;
            _albumLabels[i] = (TextBlock)FindName($"Album{i}")!;
            _statusLabels[i] = (TextBlock)FindName($"Status{i}")!;
            _progressBars[i] = (ProgressBar)FindName($"Progress{i}")!;
            _progressLabels[i] = (TextBlock)FindName($"ProgressText{i}")!;
        }
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += (_, _) => UpdateDisplay();
        _perfTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _perfTimer.Tick += (_, _) => RefreshMemoryInfo();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 页面不可见（切到其他导航页 / 窗口隐藏到托盘）时暂停刷新
        if (e.NewValue is true)
        {
            UpdateDisplay(true);
            _updateTimer.Start();
            _perfTimer.Start();
            RefreshMemoryInfo();
        }
        else
        {
            _updateTimer.Stop();
            _perfTimer.Stop();
        }
    }

    private void RefreshMemoryInfo()
    {
        try
        {
            var memoryInfo = PerformanceMonitor.GetMemoryInfo();
            var cacheStats = PerformanceMonitor.GetCacheStatistics();
            MemoryInfoText.Text =
                $"ОЗУ (рабочий набор): {memoryInfo.GetFormattedWorkingSet()}, приватная: {memoryInfo.GetFormattedPrivateMemory()}, виртуальная: {memoryInfo.GetFormattedVirtualMemory()}\n" +
                $"Управляемая память GC: {memoryInfo.GetFormattedGcMemory()}\n" +
                $"Кэш обложек: {cacheStats.ImageCacheCount} | Кэш модулей: {cacheStats.ModuleCacheCount + cacheStats.ProcessModuleCacheCount}\n" +
                $"Обновлено: {memoryInfo.Timestamp:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            MemoryInfoText.Text = $"Не удалось получить данные о памяти: {ex.Message}";
        }
    }

    private void RefreshMemoryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshMemoryInfo();
    }

    private void UpdateDisplay(bool forceRefresh = false)
    {
        try
        {
            var rpcManager = AppServices.Rpc;
            if (rpcManager == null) return;
            var allPlayersStatus = rpcManager.GetAllPlayersStatus();
            var count = Math.Min(PlayerCount, allPlayersStatus.Length);
            for (var i = 0; i < count; i++)
            {
                var (playerInfo, playerName, isActive, lastError) = allPlayersStatus[i];
                UpdatePlayerDisplay(i, playerInfo, playerName, isActive, forceRefresh, lastError);
            }
            ImageCacheManager.SetActiveKeys(_currentCacheKeys.Where(k => !string.IsNullOrEmpty(k)));
            LastUpdateLabel.Text = $"Обновлено: {DateTime.Now:HH:mm:ss}";
            UpdateSteamStateLabel();
            UpdateSteamSession();
            UpdateLivePreview(allPlayersStatus);
        }
        catch (Exception ex)
        {
            LastUpdateLabel.Text = $"Ошибка обновления: {ex.Message}";
        }
    }

    private void UpdateSteamStateLabel()
    {
        var session = AppServices.Session;
        var config = Configurations.Instance.Settings;
        string text;
        string? brushKey;
        if (!config.EnableSteamSync)
        {
            text = "Синхронизация со Steam отключена";
            brushKey = "TextFillColorSecondaryBrush";
        }
        else if (session == null)
        {
            text = "Сервис Steam не запущен";
            brushKey = "SystemFillColorCriticalBrush";
        }
        else if (!session.IsLoggedOn)
        {
            text = session.IsConnected ? "Нет входа в Steam" : "Подключение к Steam...";
            brushKey = session.IsConnected ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush";
        }
        else if (config.PauseWhenPlayingGame && AppServices.Steam?.IsRealGameActive == true)
        {
            text = "Идёт игра — музыка на паузе";
            brushKey = "SystemFillColorCautionBrush";
        }
        else
        {
            text = "Steam подключён, синхронизация активна";
            brushKey = "SystemFillColorSuccessBrush";
        }
        if (SteamStateLabel.Text != text)
        {
            SteamStateLabel.Text = text;
            SetLabelBrush(SteamStateLabel, brushKey);
        }
    }

    /// <summary>Steam 会话四行状态（自播放器页迁入）。</summary>
    private void UpdateSteamSession()
    {
        var session = AppServices.Session;
        if (session == null)
        {
            SConn.Text = "Сервис не инициализирован";
            SLogon.Text = "--";
            SRealGame.Text = "--";
            SManualPause.Text = "--";
            return;
        }
        SConn.Text = session.IsConnected ? "Подключено" : "Нет подключения";
        SLogon.Text = session.IsLoggedOn ? $"Выполнен вход ({session.Username ?? "неизвестный аккаунт"})" : "Не выполнен";
        SRealGame.Text = session.IsRealGameActive ? "Запущена — музыка на паузе" : "Нет";
        SManualPause.Text = AppServices.Steam?.ManualPause == true ? "Включена (снимается в трее)" : "Нет";
    }

    /// <summary>
    /// 实时预览好友在 Steam 看到的状态文本：正在播放 → 实际格式化结果；
    /// 暂停/空闲 → 实时操作或空闲签名。所有分支与 SteamStatusManager/RpcManager 的推送逻辑保持一致。
    /// </summary>
    private void UpdateLivePreview(System.Collections.Generic.IList<(PlayerInfo? Info, string Name, bool IsActive, RpcManager.ErrorCode Error)> allPlayersStatus)
    {
        var config = Configurations.Instance.Settings;
        string preview;
        if (!config.EnableSteamSync)
        {
            preview = "Синхронизация со Steam отключена";
        }
        else if (AppServices.Session is not { IsLoggedOn: true })
        {
            preview = "Вы не вошли в Steam (друзья не видят статус)";
        }
        else if (AppServices.Steam?.ManualPause == true)
        {
            preview = "Синхронизация приостановлена вручную";
        }
        else if (config.PauseWhenPlayingGame && AppServices.Steam?.IsRealGameActive == true)
        {
            preview = "Запущена игра в Steam — статус музыки временно скрыт";
        }
        else
        {
            PlayerInfo? activeInfo = null;
            var activeName = "";
            for (var i = 0; i < allPlayersStatus.Count; i++)
            {
                if (!allPlayersStatus[i].IsActive) continue;
                activeInfo = allPlayersStatus[i].Info;
                activeName = i < PlayerNames.Length ? PlayerNames[i] : allPlayersStatus[i].Name;
                break;
            }
            if (activeInfo is { } info)
            {
                preview = SteamStatusManager.GetStatusPreview(info, activeName, config);
            }
            else if (config.PushRealtimeActivity)
            {
                var appName = Win32Api.User32.GetForegroundAppName();
                preview = string.IsNullOrEmpty(appName)
                    ? (SteamStatusManager.GetIdleSignature(config) ?? "Простой (подпись не задана, статус скрыт)")
                    : $"Сейчас в {appName.Trim()}";
            }
            else
            {
                preview = SteamStatusManager.GetIdleSignature(config) ?? "Простой (подпись не задана, статус скрыт)";
            }
        }
        if (PreviewText.Text != preview) PreviewText.Text = preview;
    }

    private void UpdatePlayerDisplay(int index, PlayerInfo? playerInfo, string playerName, bool isActive,
        bool forceRefresh, RpcManager.ErrorCode lastError)
    {
        var lastInfo = _lastPlayerInfos[index];
        var lastActive = _lastActiveStates[index];
        if (!forceRefresh && lastActive == isActive && lastError == RpcManager.ErrorCode.None)
        {
            if (!isActive) return;
            if (lastInfo.HasValue && playerInfo.HasValue &&
                lastInfo.Value.Title == playerInfo.Value.Title &&
                lastInfo.Value.Artists == playerInfo.Value.Artists &&
                lastInfo.Value.Album == playerInfo.Value.Album &&
                lastInfo.Value.Cover == playerInfo.Value.Cover &&
                lastInfo.Value.Pause == playerInfo.Value.Pause &&
                Math.Abs(lastInfo.Value.Schedule - playerInfo.Value.Schedule) < 0.5 &&
                Math.Abs(lastInfo.Value.Duration - playerInfo.Value.Duration) < 0.5)
            {
                return;
            }
        }
        _lastPlayerInfos[index] = playerInfo;
        _lastActiveStates[index] = isActive;
        if (isActive && playerInfo != null)
        {
            const string zeroWidthSpace = "\u200B";
            var title = string.IsNullOrEmpty(playerInfo.Value.Title)
                ? StringUtils.GetTruncatedStringByMaxByteLength("Без названия", 128)
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Title + zeroWidthSpace, 128);
            var artists = string.IsNullOrEmpty(playerInfo.Value.Artists)
                ? ""
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Artists + zeroWidthSpace, 128);
            var album = string.IsNullOrEmpty(playerInfo.Value.Album)
                ? ""
                : StringUtils.GetTruncatedStringByMaxByteLength(playerInfo.Value.Album + zeroWidthSpace, 128);
            var currentSongId = playerInfo.Value.Identity;
            var previousSongId = _currentSongIds[index];
            if (!string.IsNullOrEmpty(currentSongId) && currentSongId != previousSongId)
            {
                _currentSongIds[index] = currentSongId;
            }
            if (_songTitleLabels[index].Text != title) _songTitleLabels[index].Text = title;
            var artistText = string.IsNullOrEmpty(artists) ? "" : $"🎤 {artists}";
            if (_artistLabels[index].Text != artistText) _artistLabels[index].Text = artistText;
            var albumText = string.IsNullOrEmpty(album) ? "" : $"💿 {album}";
            if (_albumLabels[index].Text != albumText) _albumLabels[index].Text = albumText;
            var statusText = playerInfo.Value.Pause ? "⏸️ На паузе" : "▶️ Играет";
            var statusBrushKey = playerInfo.Value.Pause ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush";
            if (_statusLabels[index].Text != statusText)
            {
                _statusLabels[index].Text = statusText;
                SetLabelBrush(_statusLabels[index], statusBrushKey);
            }
            if (playerInfo.Value.Duration > 0)
            {
                var progressPercentage = (int)((playerInfo.Value.Schedule / playerInfo.Value.Duration) * 100);
                var newValue = Math.Max(0, Math.Min(100, progressPercentage));
                if (_progressBars[index].Value != newValue) _progressBars[index].Value = newValue;
                var currentTime = TimeSpan.FromSeconds(playerInfo.Value.Schedule);
                var totalTime = TimeSpan.FromSeconds(playerInfo.Value.Duration);
                var progressText = $@"{currentTime:mm\:ss} / {totalTime:mm\:ss}";
                if (_progressLabels[index].Text != progressText) _progressLabels[index].Text = progressText;
            }
            else
            {
                if (_progressBars[index].Value != 0) _progressBars[index].Value = 0;
                if (_progressLabels[index].Text != "00:00 / 00:00") _progressLabels[index].Text = "00:00 / 00:00";
            }
            if (!string.IsNullOrEmpty(playerInfo.Value.Cover))
            {
                var uniqueCacheKey = string.IsNullOrEmpty(currentSongId)
                    ? playerInfo.Value.Cover
                    : $"{playerInfo.Value.Cover}_{currentSongId}";
                // 缓存保护 key 延迟到新封面真正显示时再登记（见 LoadCoverAsyncWithUniqueKey），此间旧 key 保持保护状态
                if (_currentCoverUrls[index] != playerInfo.Value.Cover)
                {
                    _currentCoverUrls[index] = playerInfo.Value.Cover;
                    Logger.Diagnose(
                        $"Updating cover for '{playerInfo.Value.Title}' (ID: {currentSongId}).");
                    Logger.Diagnose($"  - Cover URL: {playerInfo.Value.Cover}");
                    Logger.Diagnose($"  - Cache Key: {uniqueCacheKey}");
                    LoadCoverAsyncWithUniqueKey(index, playerInfo.Value.Cover, uniqueCacheKey);
                }
            }
            else
            {
                _currentCacheKeys[index] = "";
                ClearCover(index);
                _currentCoverUrls[index] = "";
            }
            if (_playerNameLabels[index].Foreground != PlayerBrushes[index])
                _playerNameLabels[index].Foreground = PlayerBrushes[index];
        }
        else
        {
            var defaultTitle = StringUtils.GetTruncatedStringByMaxByteLength("Плеер не запущен", 128);
            if (_songTitleLabels[index].Text != defaultTitle) _songTitleLabels[index].Text = defaultTitle;
            if (_artistLabels[index].Text != "") _artistLabels[index].Text = "";
            if (_albumLabels[index].Text != "") _albumLabels[index].Text = "";
            string statusText;
            switch (lastError)
            {
                case RpcManager.ErrorCode.PermissionDenied:
                    statusText = "⚠️ Нужны права администратора";
                    break;
                case RpcManager.ErrorCode.DllNotFound:
                    statusText = "⚠️ Компоненты плеера не загружены";
                    break;
                case RpcManager.ErrorCode.VersionNotSupported:
                    statusText = "⚠️ Версия не поддерживается";
                    break;
                default:
                    statusText = "Не запущен";
                    break;
            }
            if (_statusLabels[index].Text != statusText)
            {
                _statusLabels[index].Text = statusText;
                SetLabelBrush(_statusLabels[index],
                    lastError != RpcManager.ErrorCode.None
                        ? "SystemFillColorCriticalBrush"
                        : "TextFillColorSecondaryBrush");
            }
            if (_progressBars[index].Value != 0) _progressBars[index].Value = 0;
            if (_progressLabels[index].Text != "00:00 / 00:00") _progressLabels[index].Text = "00:00 / 00:00";
            ClearCover(index);
            if (_playerNameLabels[index].Foreground != null &&
                !ReferenceEquals(_playerNameLabels[index].Foreground, PlayerBrushes[index]))
            {
                _playerNameLabels[index].SetResourceReference(TextBlock.ForegroundProperty,
                    "TextFillColorSecondaryBrush");
            }
            _currentSongIds[index] = string.Empty;
            _currentCoverUrls[index] = string.Empty;
            _currentCacheKeys[index] = string.Empty;
        }
    }

    /// <summary>按主题资源键设置前景色（运行时解析，深浅色切换自动跟随）。</summary>
    private void SetLabelBrush(TextBlock label, string brushKey)
    {
        var brush = TryFindResource(brushKey) as Brush;
        if (brush != null) label.Foreground = brush;
    }

    private void ClearCover(int index)
    {
        _coverBorders[index].SetResourceReference(Border.BackgroundProperty, "ControlFillColorSecondaryBrush");
        _coverPlaceholders[index].Visibility = Visibility.Visible;
    }

    private void ShowCover(int index, BitmapSource bitmap)
    {
        _coverBorders[index].Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        _coverPlaceholders[index].Visibility = Visibility.Collapsed;
    }

    private async void LoadCoverAsyncWithUniqueKey(int index, string coverUrl, string forceCacheKey)
    {
        try
        {
            var image = await ImageCacheManager.LoadImageAsync(forceCacheKey, coverUrl);
            if (image == null) return;
            // GDI PNG 编码 + 解码 + Freeze 全在后台线程，UI 线程只做赋值
            var bitmap = await WpfImageHelper.ToFrozenBitmapAsync(image);
            if (bitmap == null)
            {
                await Dispatcher.InvokeAsync(() => ClearCover(index));
                return;
            }
            await Dispatcher.InvokeAsync(() =>
            {
                if (coverUrl == _currentCoverUrls[index])
                {
                    ShowCover(index, bitmap);
                    _currentCacheKeys[index] = forceCacheKey;
                }
                else
                {
                    Logger.Diagnose($"Stale cover update ignored for URL: {coverUrl}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load cover image with timestamp: {ex.Message}");
            await Dispatcher.InvokeAsync(() => ClearCover(index));
        }
    }
}
