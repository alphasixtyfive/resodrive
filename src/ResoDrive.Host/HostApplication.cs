using ResoDrive.Windows;

namespace ResoDrive.Host;

public static class HostApplication
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var paths = new ApplicationPaths();
        using var instanceMutex = new Mutex(
            initiallyOwned: true,
            $"Local\\{HostProtocol.GetPipeName(paths)}",
            out var createdNew);
        if (!createdNew)
            return;

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(paths);
        builder.Services.AddHostedService<Worker>();
        await builder.Build().RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
