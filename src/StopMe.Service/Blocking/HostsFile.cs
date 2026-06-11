using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace StopMe.Service.Blocking;

// Wraps every read/write/restore of %SystemRoot%\System32\drivers\etc\hosts.
// All StopMe-managed lines live between the two sentinel markers below, so we
// never touch any other line in the file. Original content (if any) is backed
// up to %ProgramData%\StopMe\hosts.bak on first write — Deactivate restores it.
//
// Hosts file changes require admin. The service runs as LocalSystem so this is
// fine; the UI never writes hosts directly.
[SupportedOSPlatform("windows")]
public sealed class HostsFile
{
    public const string BeginMarker = "# >>>>> StopMe block list — DO NOT EDIT — generated <<<<<";
    public const string EndMarker   = "# >>>>> StopMe block list end <<<<<";

    private static readonly string HostsPath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                     "drivers", "etc", "hosts");

    private readonly string _backupPath;
    private readonly ILogger<HostsFile> _logger;

    public HostsFile(string dataDir, ILogger<HostsFile> logger)
    {
        _logger = logger;
        _backupPath = System.IO.Path.Combine(dataDir, "hosts.bak");
    }

    public static string FilePath => HostsPath;
    public string BackupPath => _backupPath;

    // Rewrite the hosts file so the StopMe block contains exactly the supplied
    // domains pointing at 127.0.0.1. Loopback for www. variants is added
    // automatically. Returns (ok, denied): denied=true means we hit an
    // UnauthorizedAccessException (admin missing). The caller uses that to
    // decide whether retrying is worth it — repeated writes from a
    // non-elevated process will just spam errors.
    public (bool ok, bool denied) ApplyBlocklist(IReadOnlyCollection<string> domains)
    {
        try
        {
            EnsureBackup();
            var existing = File.Exists(HostsPath) ? File.ReadAllText(HostsPath, Encoding.UTF8) : string.Empty;
            var stripped = StripStopMeBlock(existing);

            var sb = new StringBuilder(stripped);
            if (stripped.Length > 0 && !stripped.EndsWith('\n')) sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(BeginMarker);
            sb.AppendLine($"# Updated {DateTime.UtcNow:O}");
            foreach (var d in domains.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var clean = NormalizeDomain(d);
                if (string.IsNullOrEmpty(clean)) continue;
                sb.AppendLine($"127.0.0.1\t{clean}");
                if (!clean.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"127.0.0.1\twww.{clean}");
            }
            sb.AppendLine(EndMarker);
            WriteAtomic(HostsPath, sb.ToString());
            return (true, false);
        }
        catch (UnauthorizedAccessException)
        {
            // The caller logs once and disables further attempts. Don't log
            // here — repeated 2s ticks would otherwise produce hundreds of
            // identical error lines in the rolling log file.
            return (false, true);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Hosts file write failed.");
            return (false, false);
        }
    }

    public (bool ok, bool denied) Restore()
    {
        try
        {
            if (File.Exists(_backupPath))
            {
                var original = File.ReadAllText(_backupPath, Encoding.UTF8);
                WriteAtomic(HostsPath, original);
                File.Delete(_backupPath);
                return (true, false);
            }
            if (File.Exists(HostsPath))
            {
                var current = File.ReadAllText(HostsPath, Encoding.UTF8);
                var stripped = StripStopMeBlock(current);
                WriteAtomic(HostsPath, stripped);
                return (true, false);
            }
            return (true, false);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, true);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Hosts file restore failed.");
            return (false, false);
        }
    }

    public static bool HasStopMeBlock()
    {
        try
        {
            if (!File.Exists(HostsPath)) return false;
            var content = File.ReadAllText(HostsPath, Encoding.UTF8);
            return content.Contains(BeginMarker, StringComparison.Ordinal);
        }
        catch (IOException) { return false; }
    }

    private void EnsureBackup()
    {
        if (File.Exists(_backupPath)) return;
        if (!File.Exists(HostsPath))
        {
            File.WriteAllText(_backupPath, string.Empty);
            return;
        }
        var current = File.ReadAllText(HostsPath, Encoding.UTF8);
        // If a StopMe block is already present (e.g. service crashed mid-session
        // and is restarting), back up the cleaned content so Restore() truly
        // reverts to the pre-StopMe state.
        var clean = StripStopMeBlock(current);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_backupPath)!);
        File.WriteAllText(_backupPath, clean, new UTF8Encoding(false));
    }

    internal static string StripStopMeBlock(string content)
    {
        var beginIdx = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (beginIdx < 0) return content;
        var endIdx = content.IndexOf(EndMarker, beginIdx, StringComparison.Ordinal);
        if (endIdx < 0) return content[..beginIdx].TrimEnd() + Environment.NewLine;
        var afterEnd = endIdx + EndMarker.Length;
        // Skip the trailing line break so we don't leave a blank line.
        if (afterEnd < content.Length && content[afterEnd] == '\r') afterEnd++;
        if (afterEnd < content.Length && content[afterEnd] == '\n') afterEnd++;
        var before = content[..beginIdx].TrimEnd();
        var after = afterEnd < content.Length ? content[afterEnd..] : string.Empty;
        return before + Environment.NewLine + after;
    }

    internal static string NormalizeDomain(string d)
    {
        if (string.IsNullOrWhiteSpace(d)) return string.Empty;
        var s = d.Trim().ToLowerInvariant();
        if (s.StartsWith("https://", StringComparison.Ordinal)) s = s[8..];
        else if (s.StartsWith("http://", StringComparison.Ordinal)) s = s[7..];
        var slash = s.IndexOf('/', StringComparison.Ordinal);
        if (slash > 0) s = s[..slash];
        // Wildcards / paths can't go into the hosts file. We drop them — the
        // browser extension handles those.
        if (s.Contains('*', StringComparison.Ordinal)) return string.Empty;
        if (!IsLikelyHostname(s)) return string.Empty;
        return s;
    }

    private static bool IsLikelyHostname(string s)
    {
        if (s.Length == 0 || s.Length > 253) return false;
        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c == '.' || c == '-')) return false;
        }
        return s.Contains('.', StringComparison.Ordinal);
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        // Hosts file should preserve attributes (read-only sometimes set). Drop ro.
        var attrs = File.GetAttributes(path);
        if ((attrs & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
        File.Copy(tmp, path, overwrite: true);
        File.Delete(tmp);
    }

    // Flush the DNS resolver cache so the new mappings take effect immediately.
    // Without this, browsers and other apps can keep using cached IPs for a
    // while after we change hosts.
    public void FlushDns()
    {
        try
        {
            var hr = DnsFlushResolverCache();
            if (!hr) _logger.LogWarning("DnsFlushResolverCache returned false.");
        }
        catch (DllNotFoundException ex)
        {
            _logger.LogWarning(ex, "DNSAPI not available; skipping DNS flush.");
        }
    }

    [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DnsFlushResolverCache();
}
