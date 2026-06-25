using TuneTagger.Api.Models;
using System.Diagnostics;//System.Diagnostics nos permite ejecutar procesos externos desde C#, en caso de esta aplicación fpcalc.exe

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
app.MapPost("/api/tracks/analyze", async (IFormFile file, IConfiguration configuration) =>
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

    var fpcalcPath = configuration["Fingerprinting:FpcalcPath"]; // Obtener la ruta del ejecutable fpcalc.exe desde la configuración. Actualmente está pensada para desarrollo en Windows, pero a futuro cambiará para Linux, pues se usará docker y se instalará fpcalc en la imagen de docker.

    // Verificar si la ruta de fpcalc está configurada
    if (string.IsNullOrWhiteSpace(fpcalcPath))
    {
        return Results.Problem("fpcalc path is not configured, please read the README.md file located in backed/TuneTagger.Api/Tools for instructions on how to set it up.");
    }

    // Verificar si la ruta de fpcalc es relativa y convertirla a una ruta absoluta basada en el directorio base de la aplicación
    if (!Path.IsPathRooted(fpcalcPath))
    {
        fpcalcPath = Path.Combine(AppContext.BaseDirectory, fpcalcPath);
    }

    // Verificar si el archivo fpcalc existe en la ruta especificada
    if (!File.Exists(fpcalcPath))
    {
        return Results.Problem($"fpcalc was not found at path: {fpcalcPath}");
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

        // Configurar la información de inicio del proceso para ejecutar fpcalc.exe con el archivo temporal como argumento
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fpcalcPath,
            Arguments = $"\"{tempFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Iniciar el proceso fpcalc.exe y capturar su salida
        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            return Results.Problem("Could not start fpcalc process.");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Verificar si el proceso fpcalc.exe terminó con un código de salida distinto de cero, lo que indica un error
        if (process.ExitCode != 0)
        {
            return Results.Problem($"fpcalc failed: {error}");
        }

        var lines = output.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries
        );

        // Extraer la duración y la huella digital (fingerprint) de la salida de fpcalc.exe
        var duration = lines
            .FirstOrDefault(line => line.StartsWith("DURATION="))
            ?.Replace("DURATION=", "");

        var fingerprint = lines
            .FirstOrDefault(line => line.StartsWith("FINGERPRINT="))
            ?.Replace("FINGERPRINT=", "");

        if (string.IsNullOrWhiteSpace(duration) || string.IsNullOrWhiteSpace(fingerprint))
        {
            return Results.Problem("Could not parse fpcalc output.");
        }

        //  Devolver la información del archivo subido junto con la duración y la huella digital obtenidas de fpcalc.exe
        return Results.Ok(new
        {
            originalFileName = file.FileName,
            contentType = file.ContentType,
            sizeInBytes = file.Length,
            duration,
            fingerprint,
            status = "fingerprinted"
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