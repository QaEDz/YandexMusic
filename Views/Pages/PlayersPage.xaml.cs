using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Input;
using MuSync.Models;
using MuSync.Utils;
namespace MuSync;
/// <summary>
/// 播放器页：三个播放器的轮询详情（运行状态/错误码/当前歌曲）与 Steam 会话状态。
/// 纯只读监控页，数据全部来自 AppServices 公开属性，不触碰后台逻辑。
/// </summary>
public partial class PlayersPage : Page
{
    private const int PlayerCount = 4;
    private readonly DispatcherTimer _updateTimer;
    private readonly TextBlock[] _activeLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _stateLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _songLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _artistLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _albumLabels = new TextBlock[PlayerCount];
    private readonly TextBlock[] _progressLabels = new TextBlock[PlayerCount];

    public PlayersPage()
    {
        InitializeComponent();
        for (var i = 0; i < PlayerCount; i++)
        {
            _activeLabels[i] = (TextBlock)FindName($"P{i}Active")!;
            _stateLabels[i] = (TextBlock)FindName($"P{i}State")!;
            _songLabels[i] = (TextBlock)FindName($"P{i}Song")!;
            _artistLabels[i] = (TextBlock)FindName($"P{i}Artist")!;
            _albumLabels[i] = (TextBlock)FindName($"P{i}Album")!;
            _progressLabels[i] = (TextBlock)FindName($"P{i}Progress")!;
        }
        _updateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += (_, _) => UpdateFromServices();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            UpdateFromServices();
            _updateTimer.Start();
        }
        else
        {
            _updateTimer.Stop();
        }
    }

    private void UpdateFromServices()
    {
        try
        {
            var allPlayersStatus = AppServices.Rpc?.GetAllPlayersStatus();
            if (allPlayersStatus != null)
            {
                var count = Math.Min(PlayerCount, allPlayersStatus.Length);
                for (var i = 0; i < count; i++)
                {
                    var (playerInfo, _, isActive, lastError) = allPlayersStatus[i];
                    UpdatePlayer(i, playerInfo, isActive, lastError);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[PlayersPage] 刷新详情失败: {ex.Message}");
        }
    }

    private void UpdatePlayer(int index, PlayerInfo? playerInfo, bool isActive, RpcManager.ErrorCode lastError)
    {
        _activeLabels[index].Text = isActive ? "Запущен" : "Не запущен";
        SetForeground(_activeLabels[index],
            isActive ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush");
        var stateText = lastError switch
        {
            RpcManager.ErrorCode.PermissionDenied => "Ошибка: нужны права администратора",
            RpcManager.ErrorCode.DllNotFound => "Ошибка: компоненты плеера не загружены",
            RpcManager.ErrorCode.VersionNotSupported => "Ошибка: версия плеера не поддерживается",
            _ => "Без ошибок"
        };
        if (_stateLabels[index].Text != stateText) _stateLabels[index].Text = stateText;
        SetForeground(_stateLabels[index],
            lastError == RpcManager.ErrorCode.None ? "TextFillColorSecondaryBrush" : "SystemFillColorCriticalBrush");
        if (playerInfo is { } info)
        {
            _songLabels[index].Text = string.IsNullOrEmpty(info.Title) ? "--" : info.Title;
            _artistLabels[index].Text = string.IsNullOrEmpty(info.Artists) ? "--" : info.Artists;
            _albumLabels[index].Text = string.IsNullOrEmpty(info.Album) ? "--" : info.Album;
            _progressLabels[index].Text = info.Duration > 0
                ? $"{TimeSpan.FromSeconds(info.Schedule):mm\\:ss} / {TimeSpan.FromSeconds(info.Duration):mm\\:ss}" +
                  (info.Pause ? " (на паузе)" : "")
                : "--";
        }
        else
        {
            _songLabels[index].Text = "--";
            _artistLabels[index].Text = "--";
            _albumLabels[index].Text = "--";
            _progressLabels[index].Text = "--";
        }
    }

    private void SetForeground(TextBlock label, string brushKey)
    {
        if (TryFindResource(brushKey) is Brush brush) label.Foreground = brush;
    }
}
