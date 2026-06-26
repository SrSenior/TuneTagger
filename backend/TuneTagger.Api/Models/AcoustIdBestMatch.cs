namespace TuneTagger.Api.Models;

public record AcoustIdBestMatch(
    string Title,
    string Artist,
    string Album,
    double Confidence
);