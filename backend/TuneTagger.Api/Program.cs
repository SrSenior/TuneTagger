using TuneTagger.Api.Models;
using TuneTagger.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();// Agrega servicios para generar documentación OpenAPI (Swagger) para la API. Esto permite a los desarrolladores explorar y probar los endpoints de la API a través de una interfaz web interactiva.
builder.Services.AddScoped<FingerprintService>(); // Agrega el servicio FingerprintService al contenedor de inyección de dependencias con un tiempo de vida "scoped". Esto significa que se creará una nueva instancia del servicio para cada solicitud HTTP, lo que es útil para servicios que manejan datos específicos de la solicitud.
builder.Services.AddScoped<AcoustIdService>(); // Agrega el servicio AcoustIdService al contenedor de inyección de dependencias con un tiempo de vida "scoped". Al igual que FingerprintService, se creará una nueva instancia del servicio para cada solicitud HTTP, lo que es útil para servicios que interactúan con APIs externas o manejan datos específicos de la solicitud.

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

    var allowedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" }; // Lista de extensiones de archivo de audio permitidas
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant(); // Obtener la extensión del archivo subido y convertirla a minúsculas para comparación

    // Verificar si la extensión del archivo subido está en la lista de extensiones permitidas
    if (!allowedExtensions.Contains(fileExtension))
    {
        return Results.BadRequest(new
        {
            error = "Unsupported audio format.",
            allowedExtensions
        });
    }

    // Crear un directorio temporal para almacenar el archivo subido antes de procesarlo
    var tempDirectory = Path.Combine(Path.GetTempPath(), "TuneTagger");
    Directory.CreateDirectory(tempDirectory);

    // Generar un nombre de archivo temporal único para el archivo subido, utilizando un GUID y la extensión del archivo original
    var tempFilePath = Path.Combine(
        tempDirectory,
        $"{Guid.NewGuid()}{fileExtension}"
    );

    try
    {

        // Guardar el archivo subido en el archivo temporal
        await using (var stream = File.Create(tempFilePath))
        {
            await file.CopyToAsync(stream);
        }

        // Llamar al servicio FingerprintService para generar la huella digital del archivo de audio temporal
        var fingerprintResult = await fingerprintService.GenerateAsync(tempFilePath);        

        var bestMatch = await acoustIdService.FindBestMatchAsync(
            fingerprintResult.Duration,
            fingerprintResult.Fingerprint
        );

        // Verificar si se encontró un mejor resultado de coincidencia para el archivo de audio subido
        if (bestMatch is null)
        {
            return Results.NotFound(new
            {
                originalFileName = file.FileName,
                status = "not-found",
                message = "No match was found for this audio file."
            });
        }

        // Generar un nombre de archivo sugerido basado en el artista y el título del mejor resultado encontrado, reemplazando caracteres inválidos para nombres de archivo con guiones bajos
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
.DisableAntiforgery();// Disable CSRF protection for this endpoint since it's meant to be called from a client application


app.Run();