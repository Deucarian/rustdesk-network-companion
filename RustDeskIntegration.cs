using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Simultria.RustDeskCompanion;

internal static class RustDeskPaths
{
    public static string UserConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RustDesk", "config", "RustDesk2.toml");

    public static string LocalConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RustDesk", "config", "RustDesk_local.toml");

    public static string ServiceConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "ServiceProfiles", "LocalService", "AppData", "Roaming",
        "RustDesk", "config", "RustDesk2.toml");
}

internal static class ConnectionTargetBuilder
{
    public static string Build(TargetDefinition target, ServerProfile profile)
    {
        var id = target.RustDeskId.Trim();
        if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(['@', '?', '&']) >= 0)
        {
            throw new InvalidOperationException("The saved RustDesk ID is invalid.");
        }

        if (profile.IsPublic)
        {
            return $"{id}@public";
        }

        var server = profile.ServerAddress.Trim();
        if (string.IsNullOrWhiteSpace(server) || server.IndexOfAny(['?', '&']) >= 0)
        {
            throw new InvalidOperationException("The saved RustDesk server address is invalid.");
        }

        var key = profile.PublicKey.Trim();
        if (key.IndexOfAny(['?', '&']) >= 0)
        {
            throw new InvalidOperationException("The saved RustDesk server key is invalid.");
        }

        var keyPart = string.IsNullOrWhiteSpace(key) ? "" : $"?key={key}";
        return $"{id}@{server}{keyPart}";
    }
}

internal static class RustDeskAccountState
{
    private static readonly Regex AccessTokenPattern = new(
        "^\\s*access_token\\s*=\\s*(['\"])(?<value>.*?)\\1",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static bool HasLoginToken(string? path = null)
    {
        path ??= RustDeskPaths.LocalConfigPath;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var match = AccessTokenPattern.Match(File.ReadAllText(path));
            return match.Success && !string.IsNullOrWhiteSpace(match.Groups["value"].Value);
        }
        catch
        {
            return false;
        }
    }
}

internal static class RustDeskLauncher
{
    public static void Open(string rustDeskPath)
    {
        Process.Start(new ProcessStartInfo(rustDeskPath)
        {
            UseShellExecute = true,
        });
    }

    public static void Connect(string rustDeskPath, string connectionTarget)
    {
        var startInfo = new ProcessStartInfo(rustDeskPath)
        {
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add("--connect");
        startInfo.ArgumentList.Add(connectionTarget);
        Process.Start(startInfo);
    }
}

internal static class RustDeskTomlEditor
{
    private static readonly Regex ServerSettingLine = new(
        "^[ \\t]*(?:rendezvous_server|custom-rendezvous-server|relay-server|api-server|key)[ \\t]*=.*(?:\\r?\\n|$)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ClearCustomServer(string contents)
    {
        return ServerSettingLine.Replace(contents, "");
    }

    public static string? ReadSetting(string contents, string name)
    {
        var pattern = $"^\\s*{Regex.Escape(name)}\\s*=\\s*(['\"])(?<value>.*?)\\1";
        var match = Regex.Match(contents, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value : null;
    }
}

internal sealed record PublicSetupResult(bool Success, string Message);

internal static class RustDeskPublicProfileSetup
{
    public const string CommandName = "--prepare-public-login";

    public static bool TryHandleCommand(string[] args, out int exitCode)
    {
        if (args.Length != 1 || !string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase))
        {
            exitCode = 0;
            return false;
        }

        var result = ApplyElevated();
        if (!result.Success)
        {
            MessageBox.Show(
                result.Message,
                "RustDeskHop public setup failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        exitCode = result.Success ? 0 : 1;
        return true;
    }

    public static async Task<PublicSetupResult> RequestAsync()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new(false, "RustDeskHop could not locate its own executable.");
        }

        try
        {
            var startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            startInfo.ArgumentList.Add(CommandName);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new(false, "Windows did not start the one-time setup helper.");
            }

            await process.WaitForExitAsync();
            return process.ExitCode == 0
                ? new(true, "RustDesk is ready to show its public account sign-in.")
                : new(false, "The one-time RustDesk public setup did not complete. Any detailed error was shown by the administrator helper.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, "The Windows administrator prompt was cancelled.");
        }
        catch (Exception ex)
        {
            return new(false, $"The one-time setup could not start: {ex.Message}");
        }
    }

    private static PublicSetupResult ApplyElevated()
    {
        if (!IsAdministrator())
        {
            return new(false, "Administrator approval is required for installed RustDesk configuration.");
        }

        var rustDeskPath = RustDeskLocator.Find();
        if (rustDeskPath is null)
        {
            return new(false, "RustDesk was not found in the usual installation locations.");
        }

        var optionNames = new[] { "custom-rendezvous-server", "relay-server", "api-server", "key" };
        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var originalOptions = optionNames.ToDictionary(name => name, _ => "", StringComparer.OrdinalIgnoreCase);

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");

            foreach (var configPath in new[] { RustDeskPaths.UserConfigPath, RustDeskPaths.ServiceConfigPath })
            {
                if (!File.Exists(configPath))
                {
                    continue;
                }

                var scope = string.Equals(configPath, RustDeskPaths.ServiceConfigPath, StringComparison.OrdinalIgnoreCase)
                    ? "service"
                    : "user";
                var backupDirectory = Path.Combine(
                    Path.GetDirectoryName(configPath) ?? throw new InvalidOperationException("RustDesk's configuration directory could not be found."),
                    "RustDeskHop Backups");
                Directory.CreateDirectory(backupDirectory);
                var backupPath = Path.Combine(backupDirectory, $"RustDesk2-{scope}-{timestamp}.toml");
                File.Copy(configPath, backupPath, false);
                backups[configPath] = backupPath;

                var contents = File.ReadAllText(configPath);
                foreach (var option in optionNames)
                {
                    var originalValue = RustDeskTomlEditor.ReadSetting(contents, option);
                    if (originalValue is not null)
                    {
                        originalOptions[option] = originalValue;
                    }
                }
            }

            // RustDesk's supported management interface updates an installed client/service.
            // Clearing these options returns it to the built-in public rendezvous service.
            foreach (var option in optionNames)
            {
                RunRustDeskOption(rustDeskPath, option, "");
            }

            // Clear stale persisted values too. Some RustDesk versions leave the effective
            // top-level rendezvous server behind even after the visible option is changed.
            foreach (var configPath in new[] { RustDeskPaths.UserConfigPath, RustDeskPaths.ServiceConfigPath })
            {
                ClearConfigFile(configPath);
            }

            return new(true, "RustDesk is ready to show its public account sign-in.");
        }
        catch (Exception ex)
        {
            var restored = RestoreOriginalConfiguration(rustDeskPath, backups, originalOptions);
            var recoveryMessage = restored
                ? "The previous RustDesk configuration was restored."
                : "Automatic restore was incomplete; untouched backup files remain in RustDeskHop's application-data folder.";
            return new(false, $"RustDesk public setup failed: {ex.Message} {recoveryMessage}");
        }
    }

    private static bool RestoreOriginalConfiguration(
        string rustDeskPath,
        IReadOnlyDictionary<string, string> backups,
        IReadOnlyDictionary<string, string> originalOptions)
    {
        var restored = true;
        foreach (var backup in backups)
        {
            try
            {
                File.Copy(backup.Value, backup.Key, true);
            }
            catch
            {
                restored = false;
            }
        }

        foreach (var option in originalOptions)
        {
            try
            {
                RunRustDeskOption(rustDeskPath, option.Key, option.Value);
            }
            catch
            {
                restored = false;
            }
        }

        return restored;
    }

    private static void RunRustDeskOption(string rustDeskPath, string name, string value)
    {
        var startInfo = new ProcessStartInfo(rustDeskPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("--option");
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("RustDesk did not start its configuration command.");
        if (!process.WaitForExit(10_000))
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException($"RustDesk did not finish updating {name}.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"RustDesk rejected the {name} update (exit code {process.ExitCode}).");
        }
    }

    private static void ClearConfigFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var original = File.ReadAllText(path);
        var updated = RustDeskTomlEditor.ClearCustomServer(original);
        if (string.Equals(original, updated, StringComparison.Ordinal))
        {
            return;
        }

        var temporaryPath = path + ".rustdeskhop.tmp";
        try
        {
            File.WriteAllText(temporaryPath, updated);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup failure should not hide the actual setup result.
        }
    }
}

internal static class TailscaleState
{
    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("tailscaled")
            .Concat(Process.GetProcessesByName("tailscale-ipn"))
            .ToList();
        try
        {
            return processes.Count > 0;
        }
        finally
        {
            processes.ForEach(process => process.Dispose());
        }
    }

    public static bool TryStart()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tailscale", "tailscale-ipn.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tailscale", "tailscale-ipn.exe"),
        };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
