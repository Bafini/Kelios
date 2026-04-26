using Kelio.FunctionApp;
using Kelio.Automation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.Configure<KelioAutomationOptions>(context.Configuration.GetSection("Kelio"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<KelioAutomationOptions>>().Value);
        services.AddSingleton<KelioScheduleCoordinator>();
        services.AddSingleton<KelioAutomationService>();
    })
    .Build();

await host.RunAsync();
