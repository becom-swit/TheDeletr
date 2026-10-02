using Serilog;
using Serilog.Core;

namespace Thedeletr.Logging;

public static class AuditLog
{
    public const int RetainedFileCount = 90;

    public static DirectoryInfo DefaultFolder => new(Path.Combine(AppContext.BaseDirectory, "logs"));

    public static Logger Create(DirectoryInfo folder)
    {
        Directory.CreateDirectory(folder.FullName);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("RunId", Guid.NewGuid().ToString("N")[..8])
            .WriteTo.File(
                Path.Combine(folder.FullName, "TheDeletr-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{RunId}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
