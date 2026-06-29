using System.Diagnostics;
using TuneTagger.Api.Models;

namespace TuneTagger.Api.Services;

public class FingerprintService
{
    private readonly IConfiguration _configuration;

    public FingerprintService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<FingerprintResult> GenerateAsync(string audioFilePath)
    {
        // Obtiene la ubicación de fpcalc desde la configuración para no acoplarla al entorno de ejecución.
        var fpcalcPath = _configuration["Fingerprinting:FpcalcPath"];

        // Verifica que la ruta de fpcalc esté configurada.
        if (string.IsNullOrWhiteSpace(fpcalcPath))
        {
            throw new InvalidOperationException("fpcalc path is not configured.");
        }

        // Las rutas relativas se resuelven desde el directorio de despliegue.
        if (!Path.IsPathRooted(fpcalcPath))
        {
            fpcalcPath = Path.Combine(AppContext.BaseDirectory, fpcalcPath);
        }

        // Verifica que el ejecutable exista antes de intentar iniciar el proceso.
        if (!File.Exists(fpcalcPath))
        {
            throw new FileNotFoundException($"fpcalc was not found at path: {fpcalcPath}");
        }

        // Configura fpcalc para ejecutarse sin shell y permite capturar su salida y sus errores.
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fpcalcPath,
            Arguments = $"\"{audioFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Inicia fpcalc con el archivo de audio como argumento.
        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Could not start fpcalc process.");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Un código distinto de cero indica que fpcalc no pudo completar el análisis.
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"fpcalc failed: {error}");
        }

        var lines = output.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries
        );

        // Extrae la duración y la huella de la salida textual de fpcalc.
        var duration = lines
            .FirstOrDefault(line => line.StartsWith("DURATION="))
            ?.Replace("DURATION=", "");

        var fingerprint = lines
            .FirstOrDefault(line => line.StartsWith("FINGERPRINT="))
            ?.Replace("FINGERPRINT=", "");

        // Evita devolver un resultado incompleto si cambia la salida de fpcalc o el audio no es válido.
        if (string.IsNullOrWhiteSpace(duration) || string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new InvalidOperationException("Could not parse fpcalc output.");
        }

        return new FingerprintResult(duration, fingerprint);
    }
}
