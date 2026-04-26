# Kelios

Automation project for clocking in and out on the Saint-Gobain Kelio portal using Playwright and Azure Functions.

## What it does

The project signs into Kelio through the SSO flow and triggers the `Iniciar/Terminar` action automatically.

Current behavior:
- Morning punch is randomized in a 20-minute window before `08:50`
- Lunch punches run at `12:00` and `13:00`
- Evening punch is randomized in a 20-minute window after `18:30`
- Portuguese public holidays are skipped automatically
- Extra vacation dates and company-specific holidays can be configured through app settings
- A manual HTTP endpoint exists for triggering a punch on demand

## Projects

- `Kelio.Automation`
  Core Playwright automation logic
- `Kelio.FunctionApp`
  Azure Functions host with timer triggers and manual HTTP trigger
- `Kelio.DiagnosticRunner`
  Small console runner for local diagnostics using environment variables
- `Kelio.Okta.Playwright.Tests.csproj`
  Test project shell currently used for solution structure and Playwright dependencies

## Schedules

Azure Functions currently uses four timer functions:
- `KelioMorningTimerFunction`
- `KelioLunchOutTimerFunction`
- `KelioLunchInTimerFunction`
- `KelioEveningTimerFunction`

And one manual endpoint:
- `POST /api/kelio/punch`

## Main configuration

These settings are relevant in Azure or local development:
- `Kelio__Url`
- `Kelio__ExpectedUrl`
- `Kelio__Username`
- `Kelio__Password`
- `Kelio__Headless`
- `KelioMorningSchedule`
- `KelioLunchOutSchedule`
- `KelioLunchInSchedule`
- `KelioEveningSchedule`
- `KelioMorningRandomizationMinutes`
- `KelioEveningRandomizationMinutes`
- `KelioRandomizationSeed`
- `KelioVacationDates`
- `KelioHolidayDates`
- `WEBSITE_TIME_ZONE`

Date list format for vacations and extra holidays:
- Single day: `2026-07-03`
- Range: `2026-08-15..2026-08-29`
- Multiple values: `2026-07-03,2026-08-15..2026-08-29,2026-12-24`

## Local development

### Build

```powershell
dotnet build codex.sln
```

### Run diagnostics locally

Set the required environment variables and run:

```powershell
dotnet run --project Kelio.DiagnosticRunner/Kelio.DiagnosticRunner.csproj
```

### Run the Function App locally

Use `Kelio.FunctionApp/local.settings.example.json` as the starting template, copy it to `Kelio.FunctionApp/local.settings.json`, fill in your own values, and then start the app with your preferred Azure Functions workflow.

## Notes

- `local.settings.json` should stay out of source control.
- Secrets should stay in Azure App Settings or another secret store.
- The repo intentionally excludes build output and local runtime artifacts.
