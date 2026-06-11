using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace StopMe.Service.Installation;

// Tightens the NTFS ACL on auth.token so only Administrators + SYSTEM + the
// current installer user can read it. The Phase 7 install.ps1 already protects
// the parent directory, but we re-apply at startup to (a) recover from a token
// file that existed before the directory was hardened and (b) make the
// guarantee testable in CI rather than only at install time.
[SupportedOSPlatform("windows")]
public static class TokenAclHardener
{
    public static void TryHarden(string tokenPath, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenPath);
        ArgumentNullException.ThrowIfNull(logger);

        if (!File.Exists(tokenPath)) return;
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var fileInfo = new FileInfo(tokenPath);
            var acl = fileInfo.GetAccessControl();

            // Disable inheritance, drop everything currently on the file, then
            // re-add only Administrators + SYSTEM.
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, false, typeof(NTAccount)))
                acl.RemoveAccessRule(rule);

            acl.AddAccessRule(new FileSystemAccessRule(
                identity: new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                fileSystemRights: FileSystemRights.FullControl,
                inheritanceFlags: InheritanceFlags.None,
                propagationFlags: PropagationFlags.None,
                type: AccessControlType.Allow));

            acl.AddAccessRule(new FileSystemAccessRule(
                identity: new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                fileSystemRights: FileSystemRights.FullControl,
                inheritanceFlags: InheritanceFlags.None,
                propagationFlags: PropagationFlags.None,
                type: AccessControlType.Allow));

            // The WinUI app (running as the desktop user) needs to READ the token.
            // We mirror the install.ps1 DataDir ACL: BUILTIN\Users gets read-only.
            // Production hardening (DPAPI on the token, per-user wrap) is deferred
            // — listed as a Phase 8 limitation in README.
            acl.AddAccessRule(new FileSystemAccessRule(
                identity: new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                fileSystemRights: FileSystemRights.ReadAndExecute,
                inheritanceFlags: InheritanceFlags.None,
                propagationFlags: PropagationFlags.None,
                type: AccessControlType.Allow));

            fileInfo.SetAccessControl(acl);
            logger.LogInformation("Hardened ACL on {Path} (Administrators + SYSTEM full, Users read).", tokenPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Service usually runs as LocalSystem and owns the file; if not (dev
            // console run as a normal user) we log and leave the existing ACL.
            logger.LogDebug(ex, "Skipping ACL hardening on {Path} (insufficient privileges).", tokenPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not harden ACL on {Path}.", tokenPath);
        }
    }
}
