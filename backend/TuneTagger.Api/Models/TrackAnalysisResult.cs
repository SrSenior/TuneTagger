namespace TuneTagger.Api.Models;

public record TrackAnalysisResult(
    string OriginalFileName,
    string? Title,
    string? Artist,
    string? Album,
    string SuggestedFileName,
    double Confidence,
    string Status
);