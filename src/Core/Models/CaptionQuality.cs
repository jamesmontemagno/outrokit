namespace PodcastMetadataGenerator.Core.Models;

public enum CaptionQualitySeverity
{
    Error,
    Warning
}

public sealed record CaptionQualityFinding(
    int CueNumber,
    CaptionQualitySeverity Severity,
    string Check,
    string Message,
    bool CanAutoFix = false);

public sealed record CaptionQualityReport(
    int CueCount,
    IReadOnlyList<CaptionQualityFinding> Findings)
{
    public int ErrorCount => Findings.Count(finding => finding.Severity == CaptionQualitySeverity.Error);

    public int WarningCount => Findings.Count(finding => finding.Severity == CaptionQualitySeverity.Warning);

    public int FixableCount => Findings.Count(finding => finding.CanAutoFix);

    public bool HasIssues => Findings.Count > 0;
}

public sealed record CaptionQualityFixResult(
    string Content,
    int AppliedFixCount,
    CaptionQualityReport Report);
