using TuneTagger.Api.Models;

// Librerías utilizadas para comprimir la solicitud y procesar la respuesta de AcoustID.
using System.IO.Compression; // Permite comprimir con gzip el formulario que contiene el fingerprint.
using System.Net.Http.Headers; // Permite declarar el tipo de contenido y la codificación gzip de la solicitud.
using System.Text; // Permite convertir el formulario a bytes UTF-8 antes de comprimirlo.
using System.Text.Json; // Permite validar la respuesta JSON y extraer la mejor coincidencia.

namespace TuneTagger.Api.Services;

public class AcoustIdService
{
    // Servicio que consulta AcoustID y convierte su respuesta en el modelo usado por la API.
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

        // Verifica que la URL base de AcoustID esté configurada.
        if (string.IsNullOrWhiteSpace(acoustIdBaseUrl))
        {
            throw new InvalidOperationException("AcoustID base URL is not configured.");
        }

        // Verifica que la clave de cliente de AcoustID esté configurada.
        if (string.IsNullOrWhiteSpace(acoustIdApiKey))
        {
            throw new InvalidOperationException("AcoustID API key is not configured.");
        }

        // Prepara los campos requeridos para buscar el fingerprint y solicitar metadatos musicales.
        var formData = new Dictionary<string, string>
        {
            ["client"] = acoustIdApiKey,
            ["duration"] = duration,
            ["fingerprint"] = fingerprint,
            ["meta"] = "recordings releasegroups compress",
            ["format"] = "json"
        };

        // Codifica los campos con el formato application/x-www-form-urlencoded.
        var formBody = string.Join("&", formData.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
        ));

        // AcoustID recomienda gzip para solicitudes con fingerprints largos.
        using var content = CompressStringToGzip(formBody);

        // Envía la consulta comprimida a AcoustID.
        var acoustIdResponse = await _httpClient.PostAsync(acoustIdBaseUrl, content);

        // Conserva el cuerpo para incluir la respuesta de AcoustID en posibles errores.
        var acoustIdJson = await acoustIdResponse.Content.ReadAsStringAsync();

        // Interrumpe el análisis si AcoustID responde con un código HTTP no exitoso.
        if (!acoustIdResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"AcoustID request failed with status code {(int)acoustIdResponse.StatusCode}: {acoustIdJson}"
            );
        }

        // Analiza el JSON para validar su estructura y extraer los campos relevantes.
        using var acoustIdDocument = JsonDocument.Parse(acoustIdJson);
        var root = acoustIdDocument.RootElement;

        // AcoustID debe confirmar el éxito mediante la propiedad status.
        if (!root.TryGetProperty("status", out var acoustIdStatus) ||
            acoustIdStatus.GetString() != "ok")
        {
            throw new InvalidOperationException("AcoustID returned an invalid response.");
        }

        // Una respuesta válida sin resultados representa una pista sin coincidencias.
        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return null;
        }

        return ExtractBestMatch(results);
    }

    private static ByteArrayContent CompressStringToGzip(string formBody)
    {
        // Convierte el formulario a UTF-8 antes de comprimirlo.
        var formBodyBytes = Encoding.UTF8.GetBytes(formBody);

        // Comprime el cuerpo y mantiene abierto el MemoryStream hasta copiar sus bytes.
        byte[] compressedBody;

        using (var outputStream = new MemoryStream())
        {
            using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzipStream.Write(formBodyBytes, 0, formBodyBytes.Length);
            }

            compressedBody = outputStream.ToArray();
        }

        // Declara tanto el formato del formulario como la compresión aplicada al cuerpo.
        var content = new ByteArrayContent(compressedBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        content.Headers.ContentEncoding.Add("gzip");

        return content;
    }

    private static AcoustIdBestMatch? ExtractBestMatch(JsonElement results)
    {
        JsonElement bestResult = default;
        var bestScore = -1.0;
        var hasBestResult = false;

        // Ante un empate se conserva el primer resultado devuelto por AcoustID.
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

        // Sin una puntuación numérica no se puede elegir una coincidencia válida.
        if (!hasBestResult)
        {
            return null;
        }

        // El resultado necesita al menos una grabación para producir metadatos de la pista.
        if (!bestResult.TryGetProperty("recordings", out var recordings) ||
            recordings.ValueKind != JsonValueKind.Array ||
            recordings.GetArrayLength() == 0)
        {
            return null;
        }

        // Se usa la primera grabación del resultado con mayor puntuación.
        var recording = recordings[0];

        // Obtiene el título de la primera grabación cuando está disponible.
        var title = recording.TryGetProperty("title", out var titleProperty)
            ? titleProperty.GetString()
            : null;

        string? artist = null;

        // Usa el primer artista asociado a la grabación.
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

        // Usa el primer grupo de lanzamiento como álbum cuando está disponible.
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

        // Devuelve una representación estable aunque AcoustID omita metadatos opcionales.
        return new AcoustIdBestMatch(
            title,
            artist,
            album,
            bestScore
        );
    }
}
