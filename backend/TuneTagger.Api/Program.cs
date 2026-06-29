using TuneTagger.Api.Models;
using TuneTagger.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(); // Agrega los servicios que generan el documento OpenAPI de la API; no incluye por sí solo una interfaz Swagger UI.
builder.Services.AddScoped<FingerprintService>(); // Agrega FingerprintService al contenedor de inyección de dependencias con un tiempo de vida "scoped", por lo que se crea una instancia por solicitud HTTP.
builder.Services.AddHttpClient<AcoustIdService>(); // Registra AcoustIdService y le proporciona un HttpClient administrado para consultar la API de AcoustID.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoint de prueba para verificar que la API está funcionando correctamente.
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        app = "TuneTagger.Api"
    });
})
.WithName("GetHealth");

// Endpoint de prueba que simula el análisis de una pista y devuelve datos ficticios.
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

// Endpoint para analizar una pista de música subida por el usuario.
app.MapPost("/api/tracks/analyze", async (
    IFormFile file,
    FingerprintService fingerprintService,
    AcoustIdService acoustIdService) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new
        {
            status = "error",
            message = "No file was uploaded."
        });
    }

    var allowedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" }; // Lista de extensiones de audio permitidas.
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant(); // Normaliza la extensión para compararla sin distinguir mayúsculas.

    // Verifica si la extensión del archivo está en la lista de formatos permitidos.
    if (!allowedExtensions.Contains(fileExtension))
    {
        return Results.BadRequest(new
        {
            error = "Unsupported audio format.",
            allowedExtensions
        });
    }

    // Crea un directorio temporal para almacenar el archivo durante el procesamiento.
    var tempDirectory = Path.Combine(Path.GetTempPath(), "TuneTagger");
    Directory.CreateDirectory(tempDirectory);

    // Genera un nombre temporal único y conserva la extensión original normalizada.
    var tempFilePath = Path.Combine(
        tempDirectory,
        $"{Guid.NewGuid()}{fileExtension}"
    );

    try
    {
        // Guarda el archivo subido para que fpcalc pueda procesarlo desde el sistema de archivos.
        await using (var stream = File.Create(tempFilePath))
        {
            await file.CopyToAsync(stream);
        }

        // Genera la huella digital del archivo de audio temporal.
        var fingerprintResult = await fingerprintService.GenerateAsync(tempFilePath);

        var bestMatch = await acoustIdService.FindBestMatchAsync(
            fingerprintResult.Duration,
            fingerprintResult.Fingerprint
        );

        // Devuelve 404 cuando AcoustID no encuentra una coincidencia utilizable.
        if (bestMatch is null)
        {
            return Results.NotFound(new
            {
                originalFileName = file.FileName,
                status = "not-found",
                message = "No match was found for this audio file."
            });
        }

        // Sustituye caracteres inválidos para que el nombre sugerido pueda usarse en el sistema de archivos.
        string suggestedFileName = $"{bestMatch.Artist} - {bestMatch.Title}{fileExtension}";
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            suggestedFileName = suggestedFileName.Replace(invalidChar, '_');
        }

        return Results.Ok(new TrackAnalysisResult(
            OriginalFileName: file.FileName,
            Title: bestMatch.Title,
            Artist: bestMatch.Artist,
            Album: bestMatch.Album,
            SuggestedFileName: suggestedFileName,
            Confidence: bestMatch.Confidence,
            Status: "matched"
        ));

    }
    finally
    {
        if (File.Exists(tempFilePath))
        {
            File.Delete(tempFilePath);
        }
    }
})
.WithName("AnalyzeTrack")
// La API no usa autenticación basada en cookies ni tokens antiforgery para esta carga multipart.
.DisableAntiforgery();

app.Run();
