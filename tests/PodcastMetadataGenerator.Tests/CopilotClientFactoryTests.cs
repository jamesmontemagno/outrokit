using PodcastMetadataGenerator.Core.Copilot;

namespace PodcastMetadataGenerator.Tests;

public sealed class CopilotClientFactoryTests
{
    [Theory]
    [InlineData("SDK protocol version mismatch: SDK supports versions 4-4, but server reports version 3. Please update your SDK or server to ensure compatibility.", CopilotClientFactory.CliOutdatedMessage)]
    [InlineData("SDK protocol version mismatch: SDK supports versions 4-4, but server does not report a protocol version. Please update your server to ensure compatibility.", CopilotClientFactory.CliOutdatedMessage)]
    [InlineData("SDK protocol version mismatch: SDK supports versions 4-4, but server reports version 5. Please update your SDK or server to ensure compatibility.", CopilotClientFactory.AppOutdatedMessage)]
    public void Explains_which_side_to_update_when_protocol_versions_differ(string sdkMessage, string expected)
    {
        var failure = new InvalidOperationException("Startup failed", new InvalidOperationException(sdkMessage));

        Assert.Equal(expected, CopilotClientFactory.DescribeVersionMismatch(failure));
    }

    [Fact]
    public void Leaves_other_startup_failures_alone()
    {
        Assert.Null(CopilotClientFactory.DescribeVersionMismatch(new IOException("Pipe closed")));
    }
}
