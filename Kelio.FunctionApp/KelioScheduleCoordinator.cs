using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Kelio.FunctionApp;

public sealed class KelioScheduleCoordinator
{
    private const string StateContainerName = "kelio-schedule-state";
    private const int DefaultRandomizationMinutes = 20;

    private readonly BlobContainerClient? _stateContainer;

    public KelioScheduleCoordinator()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrWhiteSpace(connectionString)
            && !string.Equals(connectionString, "UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
        {
            _stateContainer = new BlobContainerClient(connectionString, StateContainerName);
        }
    }

    public async Task<bool> ShouldRunAsync(
        string slotName,
        DateTimeOffset localNow,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        if (IsSkippedDate(localDate))
        {
            logger.LogInformation("Kelio slot '{SlotName}' ignorado em {Date}. Ferias ou feriado.", slotName, localDate);
            return false;
        }

        if (slotName is "morning" or "evening")
        {
            var target = GetRandomizedTargetTime(slotName, localDate);
            var latestAllowed = GetSlotBaseTime(slotName) + TimeSpan.FromMinutes(GetRandomizationWindowMinutes(slotName));

            if (localNow.TimeOfDay < target)
            {
                logger.LogInformation("Kelio slot '{SlotName}' ainda nao atingiu a hora sorteada de hoje: {Target}.", slotName, target);
                return false;
            }

            if (localNow.TimeOfDay > latestAllowed)
            {
                logger.LogInformation("Kelio slot '{SlotName}' passou da janela permitida. Agora: {Now}. Limite: {LatestAllowed}.", slotName, localNow.TimeOfDay, latestAllowed);
                return false;
            }

            if (await HasCompletedAsync(slotName, localDate, cancellationToken))
            {
                logger.LogInformation("Kelio slot '{SlotName}' ja foi concluido em {Date}.", slotName, localDate);
                return false;
            }

            logger.LogInformation("Kelio slot '{SlotName}' autorizado. Hora sorteada de hoje: {Target}.", slotName, target);
            return true;
        }

        return true;
    }

    public async Task MarkCompletedAsync(
        string slotName,
        DateTimeOffset localNow,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (_stateContainer is null)
        {
            logger.LogWarning("Nao foi possivel gravar estado do slot '{SlotName}' porque AzureWebJobsStorage nao esta disponivel.", slotName);
            return;
        }

        var localDate = DateOnly.FromDateTime(localNow.DateTime);

        await _stateContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _stateContainer.GetBlobClient(GetStateBlobName(slotName, localDate));
        var payload = $$"""
            {
              "slot": "{{slotName}}",
              "date": "{{localDate:yyyy-MM-dd}}",
              "completedAt": "{{localNow:O}}"
            }
            """;

        using var stream = BinaryData.FromString(payload).ToStream();
        await blob.UploadAsync(stream, overwrite: true, cancellationToken);
    }

    public string DescribeRandomizedTime(string slotName, DateOnly date)
    {
        return GetRandomizedTargetTime(slotName, date).ToString(@"hh\:mm");
    }

    private async Task<bool> HasCompletedAsync(string slotName, DateOnly date, CancellationToken cancellationToken)
    {
        if (_stateContainer is null)
        {
            return false;
        }

        await _stateContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = _stateContainer.GetBlobClient(GetStateBlobName(slotName, date));
        try
        {
            await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static string GetStateBlobName(string slotName, DateOnly date)
    {
        return $"{date:yyyy-MM-dd}/{slotName}.json";
    }

    private static bool IsSkippedDate(DateOnly date)
    {
        if (IsConfiguredDate(date, "KelioVacationDates"))
        {
            return true;
        }

        if (IsConfiguredDate(date, "KelioHolidayDates"))
        {
            return true;
        }

        return IsPortuguesePublicHoliday(date);
    }

    private static bool IsConfiguredDate(DateOnly date, string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Contains("..", StringComparison.Ordinal))
            {
                var parts = token.Split("..", StringSplitOptions.TrimEntries);
                if (parts.Length == 2
                    && DateOnly.TryParse(parts[0], out var startDate)
                    && DateOnly.TryParse(parts[1], out var endDate)
                    && date >= startDate
                    && date <= endDate)
                {
                    return true;
                }

                continue;
            }

            if (DateOnly.TryParse(token, out var configuredDate) && configuredDate == date)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPortuguesePublicHoliday(DateOnly date)
    {
        var easterSunday = GetWesternEasterSunday(date.Year);
        var fixedHolidays = new HashSet<DateOnly>
        {
            new(date.Year, 1, 1),
            new(date.Year, 4, 25),
            new(date.Year, 5, 1),
            new(date.Year, 6, 10),
            new(date.Year, 8, 15),
            new(date.Year, 10, 5),
            new(date.Year, 11, 1),
            new(date.Year, 12, 1),
            new(date.Year, 12, 8),
            new(date.Year, 12, 25)
        };

        return fixedHolidays.Contains(date)
            || date == easterSunday.AddDays(-2)
            || date == easterSunday.AddDays(60);
    }

    private static DateOnly GetWesternEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    private static TimeSpan GetRandomizedTargetTime(string slotName, DateOnly date)
    {
        var baseTime = GetSlotBaseTime(slotName);
        var window = GetRandomizationWindowMinutes(slotName);
        var offset = GetDeterministicOffsetMinutes(slotName, date, window);

        return slotName switch
        {
            "morning" => baseTime - TimeSpan.FromMinutes(offset),
            "evening" => baseTime + TimeSpan.FromMinutes(offset),
            _ => baseTime
        };
    }

    private static TimeSpan GetSlotBaseTime(string slotName)
    {
        return slotName switch
        {
            "morning" => new TimeSpan(8, 50, 0),
            "lunch-out" => new TimeSpan(12, 0, 0),
            "lunch-in" => new TimeSpan(13, 0, 0),
            "evening" => new TimeSpan(18, 30, 0),
            _ => throw new InvalidOperationException($"Unknown Kelio slot '{slotName}'.")
        };
    }

    private static int GetRandomizationWindowMinutes(string slotName)
    {
        var variableName = slotName switch
        {
            "morning" => "KelioMorningRandomizationMinutes",
            "evening" => "KelioEveningRandomizationMinutes",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(variableName))
        {
            return 0;
        }

        return int.TryParse(Environment.GetEnvironmentVariable(variableName), out var configuredMinutes)
            ? Math.Max(0, configuredMinutes)
            : DefaultRandomizationMinutes;
    }

    private static int GetDeterministicOffsetMinutes(string slotName, DateOnly date, int maxMinutes)
    {
        if (maxMinutes <= 0)
        {
            return 0;
        }

        var seed = Environment.GetEnvironmentVariable("KelioRandomizationSeed") ?? "kelio-default-seed";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{slotName}:{date:yyyy-MM-dd}"));
        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % (uint)(maxMinutes + 1));
    }
}
