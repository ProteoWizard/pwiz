using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Pwiz.Installer.Tests;

/// <summary>
/// End-to-end tests for the Inno Setup-built <c>ProteoWizard-Sharp-Setup.exe</c>.
/// Each test runs a silent install, verifies file deployment + registry state +
/// a quick CLI smoke (<c>msconvert-sharp.exe --help</c>), then silently
/// uninstalls and verifies cleanup.
///
/// Test policy:
/// <list type="bullet">
///   <item>Skip with Inconclusive when the installer hasn't been built — these
///   tests are opt-in for contributors who've already run <c>build.ps1</c>.</item>
///   <item>Skip when a real pwiz-sharp install already exists on the machine.
///   Running tests would clobber it; we'd rather the user uninstall first
///   manually than have CI silently replace + uninstall their working copy.</item>
///   <item>Skip the per-machine test when not elevated. Setup.iss has
///   <c>PrivilegesRequired=lowest</c> + overrides allowed via command line, so
///   <c>/ALLUSERS</c> triggers UAC from a non-elevated process. We don't want
///   to prompt during a test run; require the user run from an elevated shell
///   if they want per-machine coverage.</item>
/// </list>
/// </summary>
[TestClass]
public class InstallerTests
{
    // AppId from Setup.iss is version-bound: `{guid}_{version}` so multiple
    // versions install side-by-side. Inno appends `_is1` to form the uninstall
    // subkey name. The version comes from Setup.iss at test runtime (parsed
    // from the same source so the test and the installer can't drift).
    private const string AppIdBase = "{E4F1A2B3-5C6D-7E8F-9A0B-1C2D3E4F5A6B}";
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private static string AppId(string version) => $"{AppIdBase}_{version}_is1";
    private static string UninstallKeyPath(string version) => $@"{UninstallRoot}\{AppId(version)}";

    // Files that MUST land in the install dir for the install to be considered successful.
    // Keep small + load-bearing: the three EXEs that ship plus the vendor SDK loader assembly.
    private static readonly string[] RequiredFiles = new[]
    {
        "MSConvertGUI-sharp.exe",
        "msconvert-sharp.exe",
        "seems-sharp.exe",
        "Pwiz.Vendor.Common.dll",
    };

    [TestMethod]
    public void Install_PerUser_DeploysFilesAndRegistersContextMenu()
    {
        if (!TryFindSetup(out string setupPath, out string skipReason))
        {
            Assert.Inconclusive(skipReason);
            return;
        }
        string version = ReadInstallerVersion(setupPath);
        if (SameVersionAlreadyInstalled(version, out string existingScope))
        {
            Assert.Inconclusive(
                $"Version {version} of pwiz-sharp is already installed ({existingScope}); " +
                "uninstall it (or bump the installer version) before running this test. " +
                "Different versions can coexist.");
            return;
        }

        RunInstaller(setupPath, allUsers: false);
        try
        {
            string installDir = ReadInstallDir(version, perMachine: false);
            AssertRequiredFiles(installDir);
            AssertContextMenuEntries(perMachine: false, installDir);
            AssertMsconvertSmokes(installDir);
        }
        finally
        {
            RunUninstaller(version, perMachine: false);
        }

        Assert.IsFalse(IsInstalled(version, perMachine: false),
            "Uninstall finished but the per-user uninstall registry key is still present.");
        // NOTE: we intentionally do NOT assert the Explorer context-menu verbs
        // are gone after uninstall. Setup.iss is "last-installed-wins, no
        // automatic cleanup" — the verbs are shared across versions and
        // persist after any individual version's uninstall. That's an
        // accepted orphan-key trade-off (see Setup.iss for the policy).
    }

    [TestMethod]
    public void Install_PerMachine_DeploysFilesAndRegistersContextMenu()
    {
        if (!IsElevated())
        {
            Assert.Inconclusive(
                "Per-machine install requires elevation. Rerun from an elevated shell " +
                "to exercise this test (it would otherwise trigger a UAC prompt).");
            return;
        }
        if (!TryFindSetup(out string setupPath, out string skipReason))
        {
            Assert.Inconclusive(skipReason);
            return;
        }
        string version = ReadInstallerVersion(setupPath);
        if (SameVersionAlreadyInstalled(version, out string existingScope))
        {
            Assert.Inconclusive(
                $"Version {version} of pwiz-sharp is already installed ({existingScope}); " +
                "uninstall it (or bump the installer version) before running this test. " +
                "Different versions can coexist.");
            return;
        }

        RunInstaller(setupPath, allUsers: true);
        try
        {
            string installDir = ReadInstallDir(version, perMachine: true);
            AssertRequiredFiles(installDir);
            AssertContextMenuEntries(perMachine: true, installDir);
            AssertMsconvertSmokes(installDir);
        }
        finally
        {
            RunUninstaller(version, perMachine: true);
        }

        Assert.IsFalse(IsInstalled(version, perMachine: true),
            "Uninstall finished but the per-machine uninstall registry key is still present.");
        // See note in the per-user test re: context-menu verbs.
    }

    // ----------------- Helpers -----------------

    /// <summary>
    /// Try to locate the built Setup.exe. Search order: <c>PWIZ_INSTALLER_PATH</c>
    /// env var (absolute path), then the canonical
    /// <c>pwiz-sharp/installer/build/ProteoWizard-Sharp-Setup.exe</c> relative to
    /// the test assembly. Returns false + a human-readable reason if not found.
    /// </summary>
    private static bool TryFindSetup(out string setupPath, out string reason)
    {
        string? overridePath = Environment.GetEnvironmentVariable("PWIZ_INSTALLER_PATH");
        if (!string.IsNullOrEmpty(overridePath))
        {
            if (File.Exists(overridePath))
            {
                setupPath = overridePath;
                reason = string.Empty;
                return true;
            }
            setupPath = string.Empty;
            reason = $"PWIZ_INSTALLER_PATH={overridePath} but that file doesn't exist.";
            return false;
        }

        // bin/Release/net8.0-windows -> ../../../  =  Installer.Tests dir
        // Installer.Tests -> ../  =  test dir
        // test -> ../  =  pwiz-sharp
        // pwiz-sharp/installer/build/...
        string candidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "installer", "build", "ProteoWizard-Sharp-Setup.exe"));
        if (File.Exists(candidate))
        {
            setupPath = candidate;
            reason = string.Empty;
            return true;
        }

        setupPath = string.Empty;
        reason = $"ProteoWizard-Sharp-Setup.exe not found at {candidate}. " +
                 "Run `pwsh -File pwiz-sharp/installer/build.ps1` first, " +
                 "or set PWIZ_INSTALLER_PATH to its absolute path.";
        return false;
    }

    /// <summary>
    /// Return true if pwiz-sharp <paramref name="version"/> is already
    /// installed in either HKCU or HKLM. Different versions can coexist (each
    /// has its own AppId slot), so we only care about the specific version
    /// we're about to install. Identical-version reinstalls upgrade in place
    /// in Inno, which would confuse the test's install/uninstall accounting.
    /// </summary>
    private static bool SameVersionAlreadyInstalled(string version, out string scope)
    {
        if (IsInstalled(version, perMachine: false))
        {
            scope = "per-user";
            return true;
        }
        if (IsInstalled(version, perMachine: true))
        {
            scope = "per-machine";
            return true;
        }
        scope = string.Empty;
        return false;
    }

    private static bool IsInstalled(string version, bool perMachine)
    {
        using RegistryKey? key = OpenUninstallKey(version, perMachine, writable: false);
        return key is not null;
    }

    private static string ReadInstallDir(string version, bool perMachine)
    {
        using RegistryKey? key = OpenUninstallKey(version, perMachine, writable: false);
        Assert.IsNotNull(key, "Uninstall key missing right after installer reported success.");
        string? dir = key.GetValue("InstallLocation") as string;
        Assert.IsFalse(string.IsNullOrWhiteSpace(dir),
            "Uninstall key exists but InstallLocation is empty.");
        return dir!;
    }

    private static RegistryKey? OpenUninstallKey(string version, bool perMachine, bool writable)
    {
        // Setup.iss has ArchitecturesInstallIn64BitMode=x64compatible, so Inno
        // writes its uninstall entries into the 64-bit registry view. Open the
        // matching view explicitly — running tests from a 32-bit host would
        // otherwise see the WOW6432Node copy and miss our key.
        RegistryHive hive = perMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
        using RegistryKey root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.OpenSubKey(UninstallKeyPath(version), writable: writable);
    }

    /// <summary>
    /// Parse <c>MyAppVersion</c> out of Setup.iss so the test and the
    /// installer share one source of truth. Setup.iss lives next to the
    /// Setup.exe at <c>installer/Setup.iss</c>.
    /// </summary>
    private static string ReadInstallerVersion(string setupExePath)
    {
        string iss = Path.Combine(
            Path.GetDirectoryName(setupExePath) ?? "",
            "..",
            "Setup.iss");
        iss = Path.GetFullPath(iss);
        Assert.IsTrue(File.Exists(iss),
            $"Expected Setup.iss next to Setup.exe at {iss} — installer staging layout has shifted?");
        foreach (string line in File.ReadAllLines(iss))
        {
            // Line shape: `#define MyAppVersion "0.1.0"`
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#define ")) continue;
            if (!trimmed.Contains("MyAppVersion")) continue;
            int firstQuote = trimmed.IndexOf('"');
            int lastQuote = trimmed.LastIndexOf('"');
            if (firstQuote < 0 || lastQuote <= firstQuote) continue;
            return trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }
        throw new InvalidOperationException(
            $"Could not parse MyAppVersion from {iss} — line format changed?");
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Run Setup.exe silently. <c>/CURRENTUSER</c> picks per-user (no UAC),
    /// <c>/ALLUSERS</c> picks per-machine (assumes already-elevated host).
    /// <c>/TASKS=""</c> disables every optional task by default; tests that
    /// need context-menu entries override.
    /// </summary>
    private static void RunInstaller(string setupPath, bool allUsers)
    {
        string scope = allUsers ? "/ALLUSERS" : "/CURRENTUSER";
        // Enable just the two context-menu tasks so the post-install verification
        // can check the corresponding registry writes happened. Skip shortcut tasks
        // (Start Menu / Desktop) — they're orthogonal to deployment correctness and
        // pollute the host's start menu during a test run.
        string tasks = "\"context_msconvertgui,context_seems\"";
        var psi = new ProcessStartInfo(setupPath)
        {
            Arguments = $"{scope} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS={tasks}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Setup.exe");
        if (!proc.WaitForExit(milliseconds: 5 * 60_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            Assert.Fail("Installer did not exit within 5 minutes.");
        }
        Assert.AreEqual(0, proc.ExitCode,
            $"Installer returned non-zero exit code {proc.ExitCode}.");
    }

    /// <summary>
    /// Locate the uninstaller via the registry's UninstallString, invoke it
    /// silently, and poll until the install dir is gone. Inno's uninstaller
    /// copies itself to %TEMP% and re-execs, so the parent process exits
    /// almost immediately — polling for the directory's disappearance is the
    /// reliable completion signal.
    /// </summary>
    private static void RunUninstaller(string version, bool perMachine)
    {
        using RegistryKey? key = OpenUninstallKey(version, perMachine, writable: false);
        if (key is null) return; // already uninstalled
        string? uninstallString = key.GetValue("UninstallString") as string;
        if (string.IsNullOrWhiteSpace(uninstallString)) return;
        string? installDir = key.GetValue("InstallLocation") as string;

        // UninstallString is quoted: `"C:\path\to\unins000.exe"`
        string trimmed = uninstallString.Trim('"');
        var psi = new ProcessStartInfo(trimmed)
        {
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start uninstaller.");
        proc.WaitForExit(milliseconds: 2 * 60_000);

        // Poll for completion. Inno's uninstaller spawns a child copy of itself
        // from %TEMP% and the parent exits — so proc.HasExited is not the real
        // signal. Watch for the uninstall key to be deleted instead.
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            if (!IsInstalled(version, perMachine)) return;
            Thread.Sleep(500);
        }
        Assert.Fail($"Uninstaller did not complete within 3 minutes; install dir still at {installDir}.");
    }

    private static void AssertRequiredFiles(string installDir)
    {
        Assert.IsTrue(Directory.Exists(installDir),
            $"Install directory {installDir} does not exist after install.");
        foreach (string file in RequiredFiles)
        {
            string full = Path.Combine(installDir, file);
            Assert.IsTrue(File.Exists(full),
                $"Required file missing from install: {full}");
        }
    }

    /// <summary>
    /// Verify the four Explorer context-menu verbs (file + folder × MSConvertGUI +
    /// SeeMS) are registered AND their command values point at THIS install's
    /// EXEs. The latter is the load-bearing check for multi-version installs —
    /// each install overwrites the shared verb's command to its own version,
    /// and this assertion proves the overwrite happened.
    /// </summary>
    private static void AssertContextMenuEntries(bool perMachine, string installDir)
    {
        RegistryHive hive = perMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
        using RegistryKey root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        // (verb command path, exe name we expect the command to reference)
        var expected = new (string Path, string Exe)[]
        {
            (@"Software\Classes\*\shell\OpenWithMSConvertGUI\command",        "MSConvertGUI-sharp.exe"),
            (@"Software\Classes\*\shell\OpenWithSeeMS\command",               "seems-sharp.exe"),
            (@"Software\Classes\Directory\shell\OpenWithMSConvertGUI\command", "MSConvertGUI-sharp.exe"),
            (@"Software\Classes\Directory\shell\OpenWithSeeMS\command",        "seems-sharp.exe"),
        };
        foreach (var (path, exe) in expected)
        {
            using RegistryKey? key = root.OpenSubKey(path);
            Assert.IsNotNull(key,
                $"Missing context-menu verb registry key after install: {path}");
            string? cmd = key.GetValue(null) as string;
            Assert.IsFalse(string.IsNullOrWhiteSpace(cmd),
                $"Context-menu verb {path} has no default value.");
            string expectedExePath = Path.Combine(installDir, exe);
            StringAssert.Contains(cmd, expectedExePath,
                $"Context-menu verb {path} command points elsewhere — last-installed-wins overwrite didn't happen. " +
                $"Expected to contain: {expectedExePath}. Actual: {cmd}");
        }
    }

    /// <summary>
    /// End-to-end smoke: run the installed msconvert against a real vendor
    /// fixture (smallest Thermo .raw in the test corpus, ~78 KB) and verify
    /// it produces a non-empty mzML containing at least one spectrum. This
    /// exercises the full installed stack:
    /// <list type="bullet">
    ///   <item>.NET 8 desktop runtime is found and the deps.json resolves cleanly</item>
    ///   <item>VendorSdkLoader's AssemblyLoadContext hook fires and lazily fetches
    ///   ThermoFisher.CommonCore.* from raw.githubusercontent.com (first run only)</item>
    ///   <item>Reader_Thermo + SpectrumList_Thermo open the .raw and walk its scans</item>
    ///   <item>MzmlWriter serializes a valid mzML to disk</item>
    /// </list>
    /// Skips with Inconclusive if the Thermo test data isn't available — keeps
    /// the test usable on machines without the full vendor data tree.
    /// </summary>
    private static void AssertMsconvertSmokes(string installDir)
    {
        if (!TryFindThermoFixture(out string fixturePath, out string fixtureReason))
        {
            Assert.Inconclusive(fixtureReason);
            return;
        }
        string exe = Path.Combine(installDir, "msconvert-sharp.exe");
        string outDir = Path.Combine(
            Path.GetTempPath(),
            $"pwiz-sharp-installer-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                // --mzML is the default, but explicit is clearer. -o picks the output dir,
                // --outfile pins the filename so we know what to look for.
                Arguments = $"\"{fixturePath}\" --mzML -o \"{outDir}\" --outfile out.mzML",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to start {exe}");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            // 3 min wall-clock: cold-cache vendor SDK fetch + extract usually finishes in
            // ~3-10s but we leave headroom for slow networks on CI.
            Assert.IsTrue(proc.WaitForExit(milliseconds: 3 * 60_000),
                $"msconvert-sharp.exe did not exit within 3 minutes. " +
                $"stdout=<{stdout}> stderr=<{stderr}>");
            Assert.AreEqual(0, proc.ExitCode,
                $"msconvert-sharp.exe exited with code {proc.ExitCode}. " +
                $"stdout=<{stdout}> stderr=<{stderr}>");

            string outFile = Path.Combine(outDir, "out.mzML");
            Assert.IsTrue(File.Exists(outFile),
                $"msconvert reported success but no mzML was written at {outFile}. " +
                $"stdout=<{stdout}> stderr=<{stderr}>");
            string mzml = File.ReadAllText(outFile);
            StringAssert.Contains(mzml, "<mzML",
                "Output file exists but doesn't contain an <mzML> root element.");
            StringAssert.Contains(mzml, "<spectrum ",
                "Output mzML has no <spectrum> elements — the conversion produced an empty file.");
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// Locate <c>FT-HCD-MSX.raw</c> (the smallest Thermo fixture in
    /// <c>pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data/</c>). The
    /// pwiz-sharp checkout sits at <c>pwiz-msconvert-pr/pwiz-sharp/</c>;
    /// vendor test data is in the sibling <c>pwiz/</c> checkout. Override
    /// path with <c>PWIZ_THERMO_FIXTURE</c> for non-standard layouts.
    /// </summary>
    private static bool TryFindThermoFixture(out string fixturePath, out string reason)
    {
        const string fixtureName = "FT-HCD-MSX.raw";
        string? overridePath = Environment.GetEnvironmentVariable("PWIZ_THERMO_FIXTURE");
        if (!string.IsNullOrEmpty(overridePath))
        {
            if (File.Exists(overridePath))
            {
                fixturePath = overridePath;
                reason = string.Empty;
                return true;
            }
            fixturePath = string.Empty;
            reason = $"PWIZ_THERMO_FIXTURE={overridePath} but the file doesn't exist.";
            return false;
        }

        // From test bin (bin/Release/net8.0-windows): hop up to pwiz-sharp,
        // then sideways to pwiz/.
        string candidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "pwiz", "data", "vendor_readers", "Thermo",
            "Reader_Thermo_Test.data", fixtureName));
        if (File.Exists(candidate))
        {
            fixturePath = candidate;
            reason = string.Empty;
            return true;
        }

        fixturePath = string.Empty;
        reason = $"Thermo fixture {fixtureName} not found at {candidate}. " +
                 "Set PWIZ_THERMO_FIXTURE to its absolute path, or check out the " +
                 "pwiz repo as a sibling of pwiz-sharp.";
        return false;
    }
}
