using TuneTagger.Api.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint de prueba para verificar que la API está funcionando correctamente
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        app = "TuneTagger.Api"
    });
})
.WithName("GetHealth");

// Endpoint de prueba para simular el análisis de una pista de música y devolver resultados ficticios
app.MapGet("/api/tracks/mock-analysis", () =>
{
    var result = new TrackAnalysisResult(
        OriginalFileName: "Black Clover Opening 10 Full.mp3",
        Title: "Black Catcher",
        Artist: "Vickeblanka",
        Album: "Black Catcher",
        SuggestedFileName: "Vickeblanka - Black Catcher.mp3",
        Confidence: 0.94,
        Status: "mock"
    );

    return Results.Ok(result);
})
.WithName("GetMockTrackAnalysis");

// Endpoint para analizar una pista de música subida por el usuario
app.MapPost("/api/tracks/analyze", (IFormFile file) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new
        {
            status = "error",
            message = "No file was uploaded."
        });
    }

    return Results.Ok(new
    {
        originalFileName = file.FileName,
        contentType = file.ContentType,
        sizeInBytes = file.Length,
        status = "received"
    });
})
.WithName("AnalyzeTrack")
.DisableAntiforgery();// Disable CSRF protection for this endpoint since it's meant to be called from a client application


app.Run();