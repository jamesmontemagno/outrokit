using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Console.Cli;

namespace PodcastMetadataGenerator.Tests;

public sealed class PackageSkillTests
{
    private static string[] SkillFiles => Directory.GetFiles(
        Path.Combine(AppContext.BaseDirectory, "skills"), "SKILL.md", SearchOption.AllDirectories);

    [Fact]
    public void Bundled_skills_have_discoverable_names_and_trigger_descriptions()
    {
        var files = SkillFiles;
        Assert.Equal(4, files.Length);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var header = Regex.Match(content,
                @"\A---\r?\nname: (?<name>[a-z0-9]+(?:-[a-z0-9]+)*)\r?\ndescription: (?<description>[^\r\n]+)\r?\n---\r?\n");
            Assert.True(header.Success, $"Missing supported skill frontmatter: {file}");
            Assert.Equal(Path.GetFileName(Path.GetDirectoryName(file)), header.Groups["name"].Value);
            Assert.InRange(header.Groups["name"].Length, 1, 64);
            Assert.InRange(header.Groups["description"].Length, 1, 1024);
            Assert.DoesNotContain(": ", header.Groups["description"].Value);
            Assert.InRange(content.Split('\n').Length, 1, 499);
        }
    }

    [Fact]
    public async Task Every_bundled_command_example_matches_the_real_CLI()
    {
        foreach (var file in SkillFiles)
        {
            var commands = Regex.Matches(File.ReadAllText(file), @"(?ms)^```bash\r?\n(.*?)^```")
                .SelectMany(block => block.Groups[1].Value.Split('\n'))
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("outrokit ", StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(commands);
            foreach (var command in commands)
            {
                var tokens = Regex.Matches(command, "\"[^\"]*\"|\\S+")
                    .Select(token => token.Value.Trim('"')).Skip(1).ToArray();
                using var output = new StringWriter();
                using var error = new StringWriter();
                var exitCode = await new CliApplication().RunAsync([.. tokens, "--help"], output, error);
                Assert.True(exitCode == 0, $"Invalid command in {file}: {command}\n{error}");
                Assert.Contains("Usage:", output.ToString());
            }
        }
    }
}
