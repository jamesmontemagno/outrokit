using Whisper.net.Ggml;

namespace PodcastMetadataGenerator.Core.Services;

public sealed record WhisperModelOption(
    string Id,
    string DisplayName,
    string ApproximateSize,
    string Guidance,
    GgmlType GgmlType);

public static class WhisperModelCatalog
{
    public static IReadOnlyList<WhisperModelOption> All { get; } =
    [
        new("Tiny", "Tiny (multilingual)", "75 MiB", "Fastest, lowest accuracy", GgmlType.Tiny),
        new("TinyEn", "Tiny English", "75 MiB", "Fastest for English audio", GgmlType.TinyEn),
        new("Base", "Base (multilingual)", "142 MiB", "Balanced default for most machines", GgmlType.Base),
        new("BaseEn", "Base English", "142 MiB", "Balanced choice for English audio", GgmlType.BaseEn),
        new("Small", "Small (multilingual)", "466 MiB", "Better accuracy, slower inference", GgmlType.Small),
        new("SmallEn", "Small English", "466 MiB", "Better English accuracy", GgmlType.SmallEn),
        new("Medium", "Medium (multilingual)", "1.5 GiB", "High accuracy and memory use", GgmlType.Medium),
        new("MediumEn", "Medium English", "1.5 GiB", "High English accuracy and memory use", GgmlType.MediumEn),
        new("LargeV1", "Large v1", "2.9 GiB", "Legacy large multilingual model", GgmlType.LargeV1),
        new("LargeV2", "Large v2", "2.9 GiB", "Improved large multilingual model", GgmlType.LargeV2),
        new("LargeV3", "Large v3", "2.9 GiB", "Highest accuracy, highest resource use", GgmlType.LargeV3),
        new("LargeV3Turbo", "Large v3 Turbo", "1.5 GiB", "Fast high-accuracy multilingual model", GgmlType.LargeV3Turbo)
    ];

    public static WhisperModelOption Default => All[2];

    public static bool TryGet(string? id, out WhisperModelOption model)
    {
        var match = All.FirstOrDefault(option =>
            string.Equals(option.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
        model = match ?? Default;
        return match is not null;
    }

    public static WhisperModelOption Get(string? id)
    {
        TryGet(id, out var model);
        return model;
    }

    public static string GetFileName(WhisperModelOption model) => model.GgmlType switch
    {
        GgmlType.Tiny => "ggml-tiny.bin",
        GgmlType.TinyEn => "ggml-tiny.en.bin",
        GgmlType.Base => "ggml-base.bin",
        GgmlType.BaseEn => "ggml-base.en.bin",
        GgmlType.Small => "ggml-small.bin",
        GgmlType.SmallEn => "ggml-small.en.bin",
        GgmlType.Medium => "ggml-medium.bin",
        GgmlType.MediumEn => "ggml-medium.en.bin",
        GgmlType.LargeV1 => "ggml-large-v1.bin",
        GgmlType.LargeV2 => "ggml-large-v2.bin",
        GgmlType.LargeV3 => "ggml-large-v3.bin",
        GgmlType.LargeV3Turbo => "ggml-large-v3-turbo.bin",
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };
}
