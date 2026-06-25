using TuneTagger.Api.Models;
using System.Diagnostics;//System.Diagnostics nos permite ejecutar procesos externos desde C#, en caso de esta aplicación fpcalc.exe

// Librerías necesarias para la compresión de datos y el manejo de encabezados HTTP. Todo esto para el uso e integración de AcoustID
using System.IO.Compression; //System.IO.Compression nos permite trabajar con archivos comprimidos, como zip, en C#. En este caso, se utiliza para comprimir la petición POST a AcoustID, ya que la API de AcoustID requiere que los datos se envíen en formato gzip.
using System.Net.Http.Headers; //System.Net.Http.Headers nos permite trabajar con encabezados HTTP en C#. En este caso, se utiliza para establecer el tipo de compresión que vamos a usar en la petición a AcoustID
using System.Text; //System.Text nos permite trabajar con codificaciones de texto en C#. 

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

        // Verificar si se pudo extraer la duración y la huella digital correctamente
        if (string.IsNullOrWhiteSpace(duration) || string.IsNullOrWhiteSpace(fingerprint))
        {
            return Results.Problem("Could not parse fpcalc output.");
        }

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

        // Devolver un objeto JSON con la información del archivo subido, la duración, la huella digital y la respuesta de AcoustID
        return Results.Ok(new
        {
            originalFileName = file.FileName,
            contentType = file.ContentType,
            sizeInBytes = file.Length,
            duration,
            fingerprint,
            acoustIdRawResponse = acoustIdJson,
            status = "acoustid-lookup-completed"
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