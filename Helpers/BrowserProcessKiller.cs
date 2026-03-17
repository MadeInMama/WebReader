using System.Diagnostics;
using System.Runtime.InteropServices;
using PuppeteerSharp;
using WebReader.Background.AutoDownloadNewParts;

namespace WebReader.Helpers;

public static class BrowserProcessKiller
{
    private static readonly Dictionary<SupportedBrowser, Dictionary<OSPlatform, string[]>>
        BrowserProcessNames = new()
        {
            {
                SupportedBrowser.Chrome, new Dictionary<OSPlatform, string[]>
                {
                    { OSPlatform.Windows, ["chrome", "chrome_exe"] },
                    { OSPlatform.Linux, ["chrome", "google-chrome"] },
                    { OSPlatform.OSX, ["Chrome", "Google Chrome"] }
                }
            },
            {
                SupportedBrowser.Chromium, new Dictionary<OSPlatform, string[]>
                {
                    { OSPlatform.Windows, ["chrome", "chromium", "chrome_exe"] },
                    { OSPlatform.Linux, ["chromium", "chromium-browser"] },
                    { OSPlatform.OSX, ["Chromium", "Chrome"] }
                }
            },
            {
                SupportedBrowser.Firefox, new Dictionary<OSPlatform, string[]>
                {
                    { OSPlatform.Windows, ["firefox", "firefox_exe"] },
                    { OSPlatform.Linux, ["firefox", "firefox-esr"] },
                    { OSPlatform.OSX, ["Firefox"] }
                }
            }
        };

    private static int KillBrowserProcesses(SupportedBrowser browser,
        ILogger<AutoDownloadNewPartsOmniscientReader> logger, bool force = true)
    {
        var os = GetCurrentUserOs();
        var processNames = GetProcessNamesForBrowser(browser, os);
        var killedCount = 0;

        foreach (var processName in processNames)
            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                    try
                    {
                        if (force)
                            process.Kill();
                        else
                            process.CloseMainWindow();
                        process.WaitForExit(5000);
                        killedCount++;
                        logger.LogInformation("Killed: {processName} (PID: {processId})", processName, process.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation("Failed to kill {processName} (PID: {processId}): {exMessage}",
                            processName, process.Id, ex.Message);
                    }
                    finally
                    {
                        process.Dispose();
                    }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error finding {processName}: {exMessage}", processName, ex.Message);
            }

        return killedCount;
    }

    private static int KillAllBrowserProcesses(ILogger<AutoDownloadNewPartsOmniscientReader> logger, bool force = true)
    {
        return Enum.GetValues<SupportedBrowser>().Sum(browser => KillBrowserProcesses(browser, logger, force));
    }

    private static int KillAllBrowserProcesses2(ILogger<AutoDownloadNewPartsOmniscientReader> logger, bool force = true)
    {
        var totalKilled = KillAllBrowserProcesses(logger, force);

        if (totalKilled <= 0) return totalKilled;

        const int cleanupDelayMs = 2000;

        logger.LogInformation("Waiting {cleanupDelayMs}ms for processes to fully terminate...", cleanupDelayMs);
        Task.Delay(cleanupDelayMs);

        return totalKilled;
    }

    public static void PrepareCleanBrowserEnvironment(ILogger<AutoDownloadNewPartsOmniscientReader> logger)
    {
        logger.LogInformation("Preparing clean environment...");

        var killed = KillAllBrowserProcesses2(logger);

        logger.LogInformation("Killed {killed} browser process(es)", killed);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) CleanupWindowsBrowserArtifacts();
    }

    private static void CleanupWindowsBrowserArtifacts()
    {
        var tempPaths = new[]
        {
            Path.Combine(Path.GetTempPath(), "puppeteer_dev_*"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome",
                "User Data", "Default", "Lock")
        };

        foreach (var pathPattern in tempPaths)
            try
            {
                var directory = Path.GetDirectoryName(pathPattern);

                if (!Directory.Exists(directory)) continue;

                var files = Directory.GetFiles(directory, Path.GetFileName(pathPattern));
                foreach (var file in files)
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
            }
            catch
            {
            }

        Task.Delay(500);
    }

    private static OSPlatform GetCurrentUserOs()
    {
        return new List<OSPlatform?> { OSPlatform.Windows, OSPlatform.Linux, OSPlatform.OSX }
                   .FirstOrDefault(f => f.HasValue && RuntimeInformation.IsOSPlatform(f.Value)) ??
               throw new PlatformNotSupportedException("Unsupported OS");
    }

    private static string[] GetProcessNamesForBrowser(SupportedBrowser browser, OSPlatform os)
    {
        if (!BrowserProcessNames.TryGetValue(browser, out var osDict)) return [];

        return osDict.TryGetValue(os, out var names) ? names : [];
    }
}
