using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pmg-settings-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public async Task Saves_and_reloads_caption_settings()
    {
        var service = new SettingsService(_path);

        await service.SaveAsync(new AppSettings
        {
            CaptionSize = CaptionSize.Large,
            CaptionPosition = CaptionPosition.Top,
            CaptionAppearance = CaptionAppearance.BoldImpact
        });
        var loaded = await service.LoadAsync();

        Assert.Equal(CaptionSize.Large, loaded.CaptionSize);
        Assert.Equal(CaptionPosition.Top, loaded.CaptionPosition);
        Assert.Equal(CaptionAppearance.BoldImpact, loaded.CaptionAppearance);
        Assert.Contains("\"CaptionSize\": \"Large\"", await File.ReadAllTextAsync(_path));
        Assert.Contains("\"CaptionAppearance\": \"BoldImpact\"", await File.ReadAllTextAsync(_path));
    }

    [Fact]
    public async Task Settings_saved_before_captions_existed_load_with_the_defaults()
    {
        await File.WriteAllTextAsync(_path, """{ "TitleCount": 7, "FfmpegPath": "/somewhere/ffmpeg" }""");

        var loaded = await new SettingsService(_path).LoadAsync();

        Assert.Equal(CaptionSize.Medium, loaded.CaptionSize);
        Assert.Equal(CaptionPosition.Bottom, loaded.CaptionPosition);
        Assert.Equal(CaptionAppearance.ClassicOutline, loaded.CaptionAppearance);
        Assert.Equal(7, loaded.TitleCount);
        Assert.Equal("/somewhere/ffmpeg", loaded.FfmpegPath);
    }

    [Fact]
    public async Task Out_of_range_caption_settings_fall_back_without_losing_the_rest()
    {
        await File.WriteAllTextAsync(
            _path,
            """{ "CaptionSize": 42, "CaptionPosition": 9, "CaptionAppearance": 99, "TitleCount": 7 }""");

        var loaded = await new SettingsService(_path).LoadAsync();

        Assert.Equal(CaptionSize.Medium, loaded.CaptionSize);
        Assert.Equal(CaptionPosition.Bottom, loaded.CaptionPosition);
        Assert.Equal(CaptionAppearance.ClassicOutline, loaded.CaptionAppearance);
        Assert.Equal(7, loaded.TitleCount);
    }
}
