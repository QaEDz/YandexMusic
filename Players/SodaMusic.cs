using System;

namespace MuSync.Players;

/// <summary>
/// 汽水音乐（Soda Music，字节跳动）播放状态读取。
/// 汽水音乐是定制 Chromium/Electron 壳：官方无开放 API，且客户端会剥离 --remote-debugging-port
/// 等调试启动参数，CDP 与内存逆向路线均不可行。
/// 通过 Windows 系统媒体会话（SMTC）读取歌名/歌手/播放状态/时间线/封面，对用户完全透明。
/// </summary>
internal sealed class SodaMusic : SmtcMusicPlayer
{
    // 汽水音乐注册到系统媒体会话的 AppUserModelId（来自其渲染进程 --app-user-model-id 参数）。
    // 不同系统语言下理论上可能不同，附带可执行名兜底匹配。
    private const string SourceAumid = "汽水音乐";

    protected override string LogTag => "SodaMusic";

    public SodaMusic(int pid) : base(pid, "soda-covers")
    {
    }

    protected override bool IsMatchingSession(string? aumid) => IsSodaMusicAumid(aumid);

    internal static bool IsSodaMusicAumid(string? aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return false;
        if (aumid.Equals(SourceAumid, StringComparison.OrdinalIgnoreCase)) return true;
        return aumid.Contains("SodaMusic", StringComparison.OrdinalIgnoreCase) ||
               aumid.Contains("汽水音乐", StringComparison.OrdinalIgnoreCase);
    }
}
