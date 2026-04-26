using Kelio.Automation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Kelio.FunctionApp.Functions;

public sealed class KelioTimerFunction
{
    private readonly KelioAutomationService _automationService;
    private readonly KelioScheduleCoordinator _scheduleCoordinator;
    private readonly ILogger<KelioTimerFunction> _logger;

    public KelioTimerFunction(
        KelioScheduleCoordinator scheduleCoordinator,
        KelioAutomationService automationService,
        ILogger<KelioTimerFunction> logger)
    {
        _scheduleCoordinator = scheduleCoordinator;
        _automationService = automationService;
        _logger = logger;
    }

    [Function("KelioMorningTimerFunction")]
    public Task RunMorning([TimerTrigger("%KelioMorningSchedule%") ] TimerInfo timer)
    {
        return RunScheduledPunchAsync(timer, "morning");
    }

    [Function("KelioLunchOutTimerFunction")]
    public Task RunLunchOut([TimerTrigger("%KelioLunchOutSchedule%") ] TimerInfo timer)
    {
        return RunScheduledPunchAsync(timer, "lunch-out");
    }

    [Function("KelioLunchInTimerFunction")]
    public Task RunLunchIn([TimerTrigger("%KelioLunchInSchedule%") ] TimerInfo timer)
    {
        return RunScheduledPunchAsync(timer, "lunch-in");
    }

    [Function("KelioEveningTimerFunction")]
    public Task RunEvening([TimerTrigger("%KelioEveningSchedule%") ] TimerInfo timer)
    {
        return RunScheduledPunchAsync(timer, "evening");
    }

    private async Task RunScheduledPunchAsync(TimerInfo timer, string scheduleName)
    {
        if (timer.IsPastDue)
        {
            _logger.LogWarning("A execucao do trigger Kelio '{ScheduleName}' esta atrasada.", scheduleName);
        }

        var localNow = DateTimeOffset.Now;
        if (!await _scheduleCoordinator.ShouldRunAsync(scheduleName, localNow, _logger))
        {
            return;
        }

        _logger.LogInformation("Kelio timer '{ScheduleName}' iniciado em {Timestamp}.", scheduleName, DateTimeOffset.UtcNow);
        await _automationService.RunAsync();
        await _scheduleCoordinator.MarkCompletedAsync(scheduleName, localNow, _logger);
        _logger.LogInformation("Kelio timer '{ScheduleName}' terminado em {Timestamp}.", scheduleName, DateTimeOffset.UtcNow);
    }
}
