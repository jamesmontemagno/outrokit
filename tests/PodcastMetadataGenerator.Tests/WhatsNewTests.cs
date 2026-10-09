using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Console.UI;
using Spectre.Console;

namespace PodcastMetadataGenerator.Tests;

[CollectionDefinition("Console rendering", DisableParallelization = true)]
public sealed class ConsoleRenderingCollection;

[Collection("Console rendering")]
public sealed class WhatsNewTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(100)]
    public void Shows_current_details_and_compact_older_titles(int width)
    {
        var output = Normalize(Render(width));

        foreach (var item in WhatsNew.Releases[0].Items)
        {
            Assert.Contains(Normalize(item.Title), output);
            Assert.Contains(Normalize(item.Description), output);
        }

        foreach (var release in WhatsNew.Releases.Skip(1).Take(2))
        {
            Assert.Contains(release.Heading, output);
            foreach (var item in release.Items)
            {
                Assert.Contains(Normalize(item.Title), output);
                Assert.DoesNotContain(Normalize(item.Description), output);
            }
        }

        foreach (var release in WhatsNew.Releases.Skip(3))
        {
            Assert.DoesNotContain(release.Heading, output);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(100)]
    public void Respects_release_limit(int maxReleases)
    {
        var output = Render(100, maxReleases);

        for (var index = 0; index < WhatsNew.Releases.Count; index++)
        {
            if (index < maxReleases)
                Assert.Contains(WhatsNew.Releases[index].Heading, output);
            else
                Assert.DoesNotContain(WhatsNew.Releases[index].Heading, output);
        }
    }

    [Fact]
    public void Older_titles_have_no_blank_rows_between_them()
    {
        var lines = Render(100).Split('\n');

        foreach (var release in WhatsNew.Releases.Skip(1).Take(2))
        {
            var previousLine = -1;
            foreach (var item in release.Items)
            {
                var line = Array.FindIndex(lines, text => text.Contains(item.Title));
                Assert.True(line >= 0, $"Missing title: {item.Title}");
                if (previousLine >= 0)
                    Assert.Equal(previousLine + 1, line);
                previousLine = line;
            }
        }
    }

    private static string Render(int width, int maxReleases = 3)
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = width;
        var originalConsole = AnsiConsole.Console;
        try
        {
            AnsiConsole.Console = console;
            ConsoleUI.ShowWhatsNew(maxReleases);
            return writer.ToString();
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private static string Normalize(string value) => Regex.Replace(value, @"\s+", " ").Trim();
}
