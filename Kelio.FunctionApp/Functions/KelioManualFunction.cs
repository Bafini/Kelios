using Kelio.Automation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Kelio.FunctionApp.Functions;

public sealed class KelioManualFunction
{
    private readonly KelioAutomationService _automationService;
    private readonly ILogger<KelioManualFunction> _logger;

    public KelioManualFunction(
        KelioAutomationService automationService,
        ILogger<KelioManualFunction> logger)
    {
        _automationService = automationService;
        _logger = logger;
    }

    [Function("KelioManualFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "kelio/punch")] HttpRequestData request)
    {
        _logger.LogInformation("Kelio manual punch iniciado em {Timestamp}.", DateTimeOffset.UtcNow);
        await _automationService.RunAsync();
        _logger.LogInformation("Kelio manual punch terminado em {Timestamp}.", DateTimeOffset.UtcNow);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Kelio manual punch completed.");
        return response;
    }
}
