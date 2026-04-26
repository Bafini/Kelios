namespace Kelio.Automation;

public sealed class KelioAutomationOptions
{
    public string Url { get; set; } = "https://saintgobain-spm.kelio.io/open/bwt/portail.jsp#index";
    public string ExpectedUrl { get; set; } = "kelio.io";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool Headless { get; set; } = true;
}
