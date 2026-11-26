using MuSync.Players;
using Xunit;

namespace MuSync.Tests;

public class SmtcPlayerMatchingTests
{
    [Theory]
    [InlineData("ru.yandex.desktop.music")]
    [InlineData("RU.YANDEX.DESKTOP.MUSIC")]
    [InlineData("A025C540.Yandex.Music_vfvw9svesycw6!App")]
    [InlineData("Yandex.Music")]
    [InlineData("YandexMusic.exe")]
    [InlineData("Яндекс Музыка")]
    [InlineData("Яндекс Музыка.exe")]
    [InlineData(@"C:\Users\user\AppData\Local\Programs\YandexMusic\Яндекс Музыка.exe")]
    public void YandexMusic_MatchesKnownAumids(string aumid)
    {
        Assert.True(YandexMusic.IsYandexMusicAumid(aumid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Chrome")]
    [InlineData("msedge.exe")]
    [InlineData("Yandex.Browser")]
    [InlineData("Spotify.exe")]
    [InlineData("汽水音乐")]
    [InlineData("SodaMusic.exe")]
    public void YandexMusic_RejectsUnrelatedAumids(string? aumid)
    {
        Assert.False(YandexMusic.IsYandexMusicAumid(aumid));
    }

    [Theory]
    [InlineData("汽水音乐")]
    [InlineData("SodaMusic.exe")]
    [InlineData("com.luna.SodaMusic")]
    public void SodaMusic_MatchesKnownAumids(string aumid)
    {
        Assert.True(SodaMusic.IsSodaMusicAumid(aumid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ru.yandex.desktop.music")]
    [InlineData("Яндекс Музыка.exe")]
    public void SodaMusic_RejectsUnrelatedAumids(string? aumid)
    {
        Assert.False(SodaMusic.IsSodaMusicAumid(aumid));
    }
}
