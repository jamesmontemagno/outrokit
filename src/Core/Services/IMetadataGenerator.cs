using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

public interface IMetadataGenerator : IAsyncDisposable
{
    Task<List<string>> GenerateTitlesAsync(
        Transcript transcript, TitleStyle? style = null, Action<string>? onChunk = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateDescriptionAsync(
        Transcript transcript, DescriptionLength length, string? selectedTitle = null,
        Action<string>? onChunk = null, CancellationToken cancellationToken = default);

    Task<List<Chapter>> GenerateChaptersAsync(
        Transcript transcript, Action<string>? onChunk = null, CancellationToken cancellationToken = default);
}
