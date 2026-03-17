using System.Diagnostics;
using System.Runtime.InteropServices;
using PuppeteerSharp;

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

    public static int KillBrowserProcesses(SupportedBrowser browser, bool force = true)
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
                        Console.WriteLine($"Killed: {processName} (PID: {process.Id})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to kill {processName} (PID: {process.Id}): {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error finding {processName}: {ex.Message}");
            }

        return killedCount;
    }

    public static int KillAllBrowserProcesses(bool force = true)
    {
        var totalKilled = 0;
        foreach (var browser in Enum.GetValues<SupportedBrowser>()) totalKilled += KillBrowserProcesses(browser, force);
        return totalKilled;
    }

    public static int KillAllBrowserProcesses(bool force = true, int cleanupDelayMs = 1000)
    {
        var totalKilled = KillAllBrowserProcesses(force);

        if (totalKilled <= 0) return totalKilled;

        Console.WriteLine($"Waiting {cleanupDelayMs}ms for processes to fully terminate...");
        Task.Delay(cleanupDelayMs);

        return totalKilled;
    }

    public static void PrepareCleanBrowserEnvironment(SupportedBrowser browser)
    {
        Console.WriteLine($"Preparing clean environment for {browser}...");

        var killed = KillAllBrowserProcesses(true, 2000);
        Console.WriteLine($"Killed {killed} browser process(es)");

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
