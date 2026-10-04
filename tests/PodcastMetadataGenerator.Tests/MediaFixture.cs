using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

[CollectionDefinition(Name)]
public sealed class MediaCollection : ICollectionFixture<MediaFixture>
{
    public const string Name = "media";
}

/// <summary>
/// Short clips and caption files generated once with ffmpeg, so no media is checked in.
/// </summary>
public sealed class MediaFixture : IAsyncLifetime
{
    public const double ClipSeconds = 3;

    /// <summary>A moment when the test captions are on screen.</summary>
    public const double DuringCaption = 1.2;

    /// <summary>A moment after the test captions have gone.</summary>
    public const double AfterCaption = 2.6;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pmg-tests-{Guid.NewGuid():N}");

    public string Ffmpeg => TestFfmpeg.WithCaptionSupport
        ?? throw new InvalidOperationException(TestFfmpeg.MissingMessage);

    // Solid dark frames, so the only bright pixels in a burned video are caption text.
    public string Landscape => InRoot("landscape.mp4");
    public string Portrait => InRoot("portrait.mp4");
    public string Rotated => InRoot("rotated.mp4");
    public string Silent => InRoot("silent.mp4");
    public string Avi => InRoot("clip.avi");
    public string Mkv => InRoot("clip.mkv");
    public string Long => InRoot("long.mp4");
    public string AudioOnly => InRoot("audio.wav");
    public string Srt => InRoot("captions.srt");
    public string Vtt => InRoot("captions.vtt");
    public string StyledAss => InRoot("styled.ass");
    public string StyledSsa => InRoot("styled.ssa");
    public string GarbageSrt => InRoot("garbage.srt");
    public string EmptySrt => InRoot("empty.srt");

    public CaptionBurnService CreateBurnService() => new(new AppSettings { FfmpegPath = Ffmpeg });

    /// <summary>
    /// Creates an empty directory under the fixture for one test's output.
    /// </summary>
    public string NewDirectory(string name = "out")
    {
        var path = Path.Combine(_root, $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task InitializeAsync()
    {
        if (TestFfmpeg.WithCaptionSupport is null)
        {
            if (TestFfmpeg.IsRequired)
            {
                throw new InvalidOperationException(
                    $"{TestFfmpeg.RequireVariable} is set, but: {TestFfmpeg.MissingMessage}");
            }

            return;
        }

        Directory.CreateDirectory(_root);
        const string dark = "color=c=0x203040:r=25";
        const string tone = "sine=frequency=440";
        string[] h264 = ["-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p"];

        await RunAsync(["-f", "lavfi", "-i", $"{dark}:s=640x360", "-f", "lavfi", "-i", tone,
            "-t", "3", .. h264, "-c:a", "aac", Landscape]);
        await RunAsync(["-f", "lavfi", "-i", $"{dark}:s=360x640", "-t", "3", .. h264, Portrait]);
        await RunAsync(["-display_rotation", "90", "-i", Landscape, "-c", "copy", Rotated]);
        await RunAsync(["-i", Landscape, "-an", "-c", "copy", Silent]);
        await RunAsync(["-i", Landscape, "-c:v", "mpeg4", "-c:a", "pcm_s16le", Avi]);
        await RunAsync(["-i", Landscape, "-c", "copy", Mkv]);
        // Busy picture, so the encode lasts long enough to be cancelled part-way.
        await RunAsync(["-f", "lavfi", "-i", "testsrc2=s=1280x720:r=30", "-t", "60", .. h264, "-crf", "32", Long]);
        await RunAsync(["-f", "lavfi", "-i", tone, "-t", "1", AudioOnly]);

        await File.WriteAllTextAsync(Srt,
            "1\n00:00:00,500 --> 00:00:02,000\nHello captions\n");
        await File.WriteAllTextAsync(Vtt,
            "WEBVTT\n\n00:00.500 --> 00:02.000\nHello captions\n");
        await File.WriteAllTextAsync(StyledAss,
            """
            [Script Info]
            ScriptType: v4.00+
            PlayResX: 640
            PlayResY: 360

            [V4+ Styles]
            Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
            Style: Default,Arial,30,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,1,0,0,0,100,100,0,0,1,2,0,9,12,12,12,1

            [Events]
            Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
            Dialogue: 0,0:00:00.50,0:00:02.00,Default,,0,0,0,,Top right

            """);
        // The older SubStation Alpha format: its own section names, a Marked field on each line,
        // and its own alignment numbers, where 7 is top right.
        await File.WriteAllTextAsync(StyledSsa,
            """
            [Script Info]
            ScriptType: v4.00
            PlayResX: 640
            PlayResY: 360

            [V4 Styles]
            Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, TertiaryColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, AlphaLevel, Encoding
            Style: Default,Arial,30,16777215,255,0,0,-1,0,1,2,0,7,12,12,12,0,1

            [Events]
            Format: Marked, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
            Dialogue: Marked=0,0:00:00.50,0:00:02.00,Default,,0,0,0,,Top right

            """);
        await File.WriteAllTextAsync(GarbageSrt, "hello this is not captions\n");
        await File.WriteAllTextAsync(EmptySrt, string.Empty);
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Leftover temporary clips are not a test failure.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Decodes one frame of a video as 8-bit brightness values.
    /// </summary>
    public async Task<Frame> ReadFrameAsync(string videoPath, double seconds)
    {
        var details = await CreateBurnService().GetVideoDetailsAsync(videoPath);
        var rawPath = Path.Combine(_root, $"frame-{Guid.NewGuid():N}.gray");
        await RunAsync(["-i", videoPath, "-ss", seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", rawPath]);
        var pixels = await File.ReadAllBytesAsync(rawPath);
        File.Delete(rawPath);
        Assert.Equal(details.Width * details.Height, pixels.Length);
        return new Frame(details.Width, details.Height, pixels);
    }

    private Task RunAsync(string[] arguments) => TestFfmpeg.RunAsync(Ffmpeg, arguments);

    private string InRoot(string name) => Path.Combine(_root, name);
}

/// <summary>
/// One decoded video frame. Regions are given as fractions of the width and height.
/// </summary>
public sealed record Frame(int Width, int Height, byte[] Pixels)
{
    // Caption text is white; the test clips are far darker than this.
    private const byte Bright = 200;

    public int BrightPixels(double left = 0, double top = 0, double right = 1, double bottom = 1)
    {
        var count = 0;
        for (var y = (int)(top * Height); y < (int)(bottom * Height); y++)
        {
            for (var x = (int)(left * Width); x < (int)(right * Width); x++)
            {
                if (Pixels[y * Width + x] >= Bright)
                {
                    count++;
                }
            }
        }

        return count;
    }

    public int TopThird => BrightPixels(bottom: 1 / 3.0);

    public int BottomThird => BrightPixels(top: 2 / 3.0);
}

internal sealed class CallbackProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
