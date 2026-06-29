using TuneTagger.Api.Models;

// Librerías necesarias para la compresión de datos y el manejo de encabezados HTTP. Todo esto para el uso e integración de AcoustID
using System.IO.Compression; //System.IO.Compression nos permite trabajar con archivos comprimidos, como zip, en C#. En este caso, se utiliza para comprimir la petición POST a AcoustID, ya que la API de AcoustID requiere que los datos se envíen en formato gzip.
using System.Net.Http.Headers; //System.Net.Http.Headers nos permite trabajar con encabezados HTTP en C#. En este caso, se utiliza para establecer el tipo de compresión que vamos a usar en la petición a AcoustID
using System.Text; //System.Text nos permite trabajar con codificaciones de texto en C#. 

using System.Text.Json; //System.Text.Json nos permite trabajar con JSON en C#. En este caso, se utiliza para limpiar la respuesta de AcoustID.

namespace TuneTagger.Api.Services;

public class AcoustIdService
{
    // Servicio para interactuar con la API de AcoustID, que permite buscar coincidencias de huellas digitales de audio y obtener información sobre pistas de música.
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AcoustIdService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<AcoustIdBestMatch?> FindBestMatchAsync(string duration, string fingerprint)
    {
        var acoustIdBaseUrl = _configuration["AcoustId:BaseUrl"];
        var acoustIdApiKey = _configuration["AcoustId:ApiKey"];

        // Verificar si la URL base de AcoustID está configurada
        if (string.IsNullOrWhiteSpace(acoustIdBaseUrl))
        {
            throw new InvalidOperationException("AcoustID base URL is not configured.");
        }

        // Verificar si la clave de API de AcoustID está configurada
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            throw new InvalidOperationException("AcoustID API key is not configured.");
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

        // Comprimir los datos del formulario con GZip, siguiendo la recomendación de AcoustID para fingerprints largos.
        using var content = CompressStringToGzip(formBody);
        
        // Enviar la solicitud POST a la API de AcoustID con los datos comprimidos
        var acoustIdResponse = await _httpClient.PostAsync(acoustIdBaseUrl, content);
        
        // Leer la respuesta de la API de AcoustID como una cadena JSON
        var acoustIdJson = await acoustIdResponse.Content.ReadAsStringAsync();  

        // Verificar si la respuesta de la API de AcoustID indica un error (código de estado HTTP distinto de 2xx)
        if (!acoustIdResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
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
            throw new InvalidOperationException("AcoustID returned an invalid response.");
        }

        // Verificar si la respuesta de AcoustID contiene la propiedad "results" y si es un arreglo no vacío
        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return null;
        }

        return ExtractBestMatch(results);
        
    }
    
    // Método para comprimir una cadena de consulta a formato GZip, siguiendo la recomendación de AcoustID para fingerprints largos.
    private static ByteArrayContent CompressStringToGzip(string formBody)
    {
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

        // Establecer el encabezado Content-Encoding a gzip para indicar que los datos están comprimidos
        var content = new ByteArrayContent(compressedBody);

        // Establecer el tipo de contenido a application/x-www-form-urlencoded, que es el tipo de contenido esperado por la API de AcoustID
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        content.Headers.ContentEncoding.Add("gzip");
        
        return content;
    }

    private static AcoustIdBestMatch? ExtractBestMatch (JsonElement results)
    {
        
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
            return null;
        }

        // Verificar si el mejor resultado contiene la propiedad "recordings" y si es un arreglo no vacío
        if (!bestResult.TryGetProperty("recordings", out var recordings) ||
            recordings.ValueKind != JsonValueKind.Array ||
            recordings.GetArrayLength() == 0)
        {
            return null;
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

        // Devolver un objeto AcoustIdBestMatch con la información del mejor resultado encontrado
        return new AcoustIdBestMatch(
            title,
            artist,
            album,
            bestScore
        );
    }

}