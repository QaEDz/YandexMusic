using System;

namespace MuSync.Players;

/// <summary>
/// Яндекс Музыка（Yandex Music）播放状态读取。
/// 官方桌面客户端基于 Electron（Яндекс Музыка.exe），历史 Microsoft Store 版本为 UWP（A025C540.Yandex.Music），
/// 播放媒体时均会自动注册到 Windows 系统媒体会话（SMTC），通过 GlobalSystemMediaTransportControls 读取。
/// </summary>
internal sealed class YandexMusic : SmtcMusicPlayer
{
    // Electron 桌面客户端 AppUserModelId
    private const string DesktopAumid = "ru.yandex.desktop.music";

    protected override string LogTag => "YandexMusic";

    public YandexMusic(int pid) : base(pid, "yandex-covers")
    {
    }

    protected override bool IsMatchingSession(string? aumid) => IsYandexMusicAumid(aumid);

    internal static bool IsYandexMusicAumid(string? aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return false;
        if (aumid.Equals(DesktopAumid, StringComparison.OrdinalIgnoreCase)) return true;
        return aumid.Contains("ru.yandex.desktop.music", StringComparison.OrdinalIgnoreCase) ||
               aumid.Contains("Yandex.Music", StringComparison.OrdinalIgnoreCase) ||
               aumid.Contains("YandexMusic", StringComparison.OrdinalIgnoreCase) ||
               aumid.Contains("Яндекс Музыка", StringComparison.OrdinalIgnoreCase);
    }
}
