using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Zadanie_04._01;

public static class PlaywrightBootstrap
{
    public static async Task<IBrowser> LaunchChromiumAsync(
        IPlaywright playwright,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }
        catch (PlaywrightException ex) when (IsMissingBrowserError(ex))
        {
            logger.LogWarning("Brak przeglądarek Playwright — uruchamiam instalację...");
            InstallBrowsers(logger);
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
        }
    }

    public static void InstallBrowsers(ILogger logger)
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                "Nie udało się zainstalować przeglądarek Playwright. " +
                "Uruchom ręcznie: pwsh bin/Debug/net10.0/playwright.ps1 install chromium");
        }

        logger.LogInformation("Przeglądarki Playwright zainstalowane.");
    }

    private static bool IsMissingBrowserError(PlaywrightException ex)
        => ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
           || ex.Message.Contains("Please run the following command", StringComparison.OrdinalIgnoreCase);
}
