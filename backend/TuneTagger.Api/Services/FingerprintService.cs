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
        var fpcalcPath = _configuration["Fingerprinting:FpcalcPath"]; // Obtener la ruta del ejecutable fpcalc.exe desde la configuración. Actualmente está pensada para desarrollo en Windows, pero a futuro cambiará para Linux, pues se usará docker y se instalará fpcalc en la imagen de docker.

        // Verificar si la ruta de fpcalc está configurada
        if (string.IsNullOrWhiteSpace(fpcalcPath))
        {
            throw new InvalidOperationException("fpcalc path is not configured.");
        }

        // Verificar si la ruta de fpcalc es relativa y convertirla a una ruta absoluta basada en el directorio base de la aplicación
        if (!Path.IsPathRooted(fpcalcPath))
        {
            fpcalcPath = Path.Combine(AppContext.BaseDirectory, fpcalcPath);
        }

        // Verificar si el archivo fpcalc existe en la ruta especificada
        if (!File.Exists(fpcalcPath))
        {
            throw new FileNotFoundException($"fpcalc was not found at path: {fpcalcPath}");
        }

        // Configurar la información de inicio del proceso para ejecutar fpcalc.exe con el archivo temporal como argumento
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fpcalcPath,
            Arguments = $"\"{audioFilePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Iniciar el proceso fpcalc.exe y capturar su salida
        using var process = Process.Start(processStartInfo);

        if (process is null)
        {
            throw new InvalidOperationException("Could not start fpcalc process.");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        // Verificar si el proceso fpcalc.exe terminó con un código de salida distinto de cero, lo que indica un error
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"fpcalc failed: {error}");
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
            throw new InvalidOperationException("Could not parse fpcalc output.");
        }

        return new FingerprintResult(duration, fingerprint);
    }
}