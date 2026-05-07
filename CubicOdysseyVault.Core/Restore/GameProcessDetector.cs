using System.Diagnostics;

namespace CubicOdysseyVault.Core.Restore;

public static class GameProcessDetector
{
    // True if a process matching the Cubic Odyssey executable name appears in
    // the system's process list. On Linux/Proton the .exe is hosted by wine
    // and won't show as a literal Process.GetProcessesByName match — so we
    // shell out to `pgrep -f` which searches the full command line. On
    // Windows we use the managed API. Best effort: any error returns false
    // so the user isn't permanently locked out of restore by a probe failure.
    public static bool IsCubicOdysseyRunning()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return Process.GetProcessesByName(Constants.CubicOdysseyProcessName).Length > 0;

            return PgrepFullCommandLine(Constants.CubicOdysseyProcessName);
        }
        catch
        {
            return false;
        }
    }

    private static bool PgrepFullCommandLine(string needle)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pgrep",
                Arguments = $"-f {needle}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(2000))
            {
                try { p.Kill(); } catch { }
                return false;
            }
            return p.ExitCode == 0; // pgrep exits 0 iff at least one match found
        }
        catch
        {
            return false;
        }
    }
}
