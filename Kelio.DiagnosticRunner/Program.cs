using Kelio.Automation;
using Microsoft.Extensions.Logging.Abstractions;

static string GetRequiredEnvironmentVariable(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required environment variable '{name}'.");
    }

    return value;
}

var options = new KelioAutomationOptions
{
    Url = Environment.GetEnvironmentVariable("Kelio__Url")
        ?? "https://saintgobain-spm.kelio.io/open/bwt/portail.jsp#index",
    ExpectedUrl = Environment.GetEnvironmentVariable("Kelio__ExpectedUrl") ?? "kelio.io",
    Username = GetRequiredEnvironmentVariable("Kelio__Username"),
    Password = GetRequiredEnvironmentVariable("Kelio__Password"),
    Headless = bool.TryParse(Environment.GetEnvironmentVariable("Kelio__Headless"), out var headless)
        ? headless
        : true
};

await new KelioAutomationService(
        options,
        NullLogger<KelioAutomationService>.Instance)
    .RunAsync();
