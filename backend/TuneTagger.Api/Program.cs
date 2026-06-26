using TuneTagger.Api.Models;
using TuneTagger.Api.Services;

// Librerías necesarias para la compresión de datos y el manejo de encabezados HTTP. Todo esto para el uso e integración de AcoustID
using System.IO.Compression; //System.IO.Compression nos permite trabajar con archivos comprimidos, como zip, en C#. En este caso, se utiliza para comprimir la petición POST a AcoustID, ya que la API de AcoustID requiere que los datos se envíen en formato gzip.
using System.Net.Http.Headers; //System.Net.Http.Headers nos permite trabajar con encabezados HTTP en C#. En este caso, se utiliza para establecer el tipo de compresión que vamos a usar en la petición a AcoustID
using System.Text; //System.Text nos permite trabajar con codificaciones de texto en C#. 

using System.Text.Json; //System.Text.Json nos permite trabajar con JSON en C#. En este caso, se utiliza para limpiar la respuesta de AcoustID.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();// Agrega servicios para generar documentación OpenAPI (Swagger) para la API. Esto permite a los desarrolladores explorar y probar los endpoints de la API a través de una interfaz web interactiva.
builder.Services.AddScoped<FingerprintService>(); // Agrega el servicio FingerprintService al contenedor de inyección de dependencias con un tiempo de vida "scoped". Esto significa que se creará una nueva instancia del servicio para cada solicitud HTTP, lo que es útil para servicios que manejan datos específicos de la solicitud.

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
    IFormFile file, IConfiguration configuration,
    FingerprintService fingerprintService) =>
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

//Desde acá

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

        var fingerprintResult = await fingerprintService.GenerateAsync(tempFilePath);

        var duration = fingerprintResult.Duration;
        var fingerprint = fingerprintResult.Fingerprint;

        // Obtener la URL base, que tenemos en appsettings.Development.json, y la clave de API de AcoustID, que almacenamos con dotnet user-secrets.
        var acoustIdBaseUrl = configuration["AcoustId:BaseUrl"];
        var acoustIdApiKey = configuration["AcoustId:ApiKey"];

        // Verificar si la URL base de AcoustID está configurada
        if (string.IsNullOrWhiteSpace(acoustIdBaseUrl))
        {
            return Results.Problem("AcoustID base URL is not configured.");
        }

        // Verificar si la clave de API de AcoustID está configurada
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            return Results.Problem("AcoustID API key is not configured.");
        }

        // Preparar los datos del formulario para enviar a la API de AcoustID.
        var formData = new Dictionary<string, string>
        {
            ["client"] = acoustIdApiKey,
            ["duration"] = duration,
            ["fingerprint"] = fingerprint,
            ["meta"] = "recordings releasegroups compress",
            ["format"] = "json"
        };

        // Convertir los datos del formulario a una cadena de consulta URL codificada
        var formBody = string.Join("&", formData.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
        ));

        // Codificar la cadena de consulta a bytes utilizando UTF-8
        var formBodyBytes = Encoding.UTF8.GetBytes(formBody);

        // Comprimir los datos del formulario con GZip, siguiendo la recomendación de AcoustID para fingerprints largos.
        byte[] compressedBody;

        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzipStream.Write(formBodyBytes, 0, formBodyBytes.Length);
            }

            compressedBody = outputStream.ToArray();
        }

        // Crear un cliente HTTP para enviar la solicitud POST a la API de AcoustID
        using var httpClient = new HttpClient();
        // Establecer el encabezado Content-Encoding a gzip para indicar que los datos están comprimidos
        using var content = new ByteArrayContent(compressedBody);

        // Establecer el tipo de contenido a application/x-www-form-urlencoded, que es el tipo de contenido esperado por la API de AcoustID
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        content.Headers.ContentEncoding.Add("gzip");

        // Enviar la solicitud POST a la API de AcoustID y obtener la respuesta
        var acoustIdResponse = await httpClient.PostAsync(acoustIdBaseUrl, content);
        // Leer la respuesta de la API de AcoustID como una cadena JSON
        var acoustIdJson = await acoustIdResponse.Content.ReadAsStringAsync();  

        // Verificar si la respuesta de la API de AcoustID indica un error (código de estado HTTP distinto de 2xx)
        if (!acoustIdResponse.IsSuccessStatusCode)
        {
            return Results.Problem(
                $"AcoustID request failed with status code {(int)acoustIdResponse.StatusCode}: {acoustIdJson}"
            );
        }

        // Analizar la respuesta JSON de AcoustID para asegurarse de que es válida y poder devolverla en la respuesta de nuestra API
        using var acoustIdDocument = JsonDocument.Parse(acoustIdJson);
        var root = acoustIdDocument.RootElement;

        // Verificar si la respuesta de AcoustID contiene la propiedad "status" y si su valor es "ok"
        if (!root.TryGetProperty("status", out var acoustIdStatus) ||
            acoustIdStatus.GetString() != "ok")
        {
            return Results.Problem("AcoustID returned an invalid response.");
        }

        // Verificar si la respuesta de AcoustID contiene la propiedad "results" y si es un arreglo no vacío
        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return Results.NotFound(new
            {
                originalFileName = file.FileName,
                status = "not-found",
                message = "No matches were found for this audio file."
            });
        }

        JsonElement bestResult = default; // Variable para almacenar el mejor resultado de AcoustID
        var bestScore = -1.0; // Variable para almacenar la mejor puntuación de coincidencia encontrada
        var hasBestResult = false; // Variable para indicar si se ha encontrado un mejor resultado

        // Iterar sobre los resultados devueltos por AcoustID para encontrar el resultado con la mejor puntuación (score)
        foreach (var result in results.EnumerateArray())
        {
            if (result.TryGetProperty("score", out var scoreProperty) &&
                scoreProperty.TryGetDouble(out var score) &&
                score > bestScore)
            {
                bestScore = score;
                bestResult = result;
                hasBestResult = true;
            }
        }

        // Verificar si se encontró un mejor resultado con una puntuación válida
        if (!hasBestResult)
        {
            return Results.NotFound(new
            {
                originalFileName = file.FileName,
                status = "not-found",
                message = "No valid match score was found."
            });
        }

        // Verificar si el mejor resultado contiene la propiedad "recordings" y si es un arreglo no vacío
        if (!bestResult.TryGetProperty("recordings", out var recordings) ||
            recordings.ValueKind != JsonValueKind.Array ||
            recordings.GetArrayLength() == 0)
        {
            return Results.NotFound(new
            {
                originalFileName = file.FileName,
                confidence = bestScore,
                status = "not-found",
                message = "A match was found, but it did not include recording metadata."
            });
        }

        var recording = recordings[0];// Tomar el primer resultado de grabación como el mejor resultado

        // Intentar obtener el título de la grabación del resultado de AcoustID, si está disponible
        var title = recording.TryGetProperty("title", out var titleProperty)
            ? titleProperty.GetString()
            : null;
        
        string? artist = null;

        // Intentar obtener el nombre del primer artista del resultado de AcoustID, si está disponible
        if (recording.TryGetProperty("artists", out var artists) &&
            artists.ValueKind == JsonValueKind.Array &&
            artists.GetArrayLength() > 0)
        {
            var firstArtist = artists[0];

            if (firstArtist.TryGetProperty("name", out var artistNameProperty))
            {
                artist = artistNameProperty.GetString();
            }
        }

        string? album = null;

        // Intentar obtener el título del primer grupo de lanzamiento (release group) del resultado de AcoustID, si está disponible
        if (recording.TryGetProperty("releasegroups", out var releaseGroups) &&
            releaseGroups.ValueKind == JsonValueKind.Array &&
            releaseGroups.GetArrayLength() > 0)
        {
            var firstReleaseGroup = releaseGroups[0];

            if (firstReleaseGroup.TryGetProperty("title", out var albumTitleProperty))
            {
                album = albumTitleProperty.GetString();
            }
        }

        title ??= "Unknown Title";
        artist ??= "Unknown Artist";
        album ??= "Unknown Album";

        var suggestedFileName = $"{artist} - {title}{fileExtension}";

        // Reemplazar caracteres inválidos en el nombre de archivo sugerido con guiones bajos para asegurar que sea un nombre de archivo válido
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            suggestedFileName = suggestedFileName.Replace(invalidChar, '_');
        }


        // Devolver un objeto JSON con la información del archivo subido, el título, el artista, el álbum, el nombre de archivo sugerido, la confianza y el estado de coincidencia
        return Results.Ok(new
        {
            originalFileName = file.FileName,
            title,
            artist,
            album,
            suggestedFileName,
            confidence = bestScore,
            status = "matched"
        });
    
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