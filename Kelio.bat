@echo off
setlocal

dotnet test "C:\codex\Kelio.Okta.Playwright.Tests.csproj" --no-restore

endlocal
