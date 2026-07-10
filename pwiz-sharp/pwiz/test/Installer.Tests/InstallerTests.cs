using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using Pwiz.TestHarness;

namespace Pwiz.Installer.Tests;

/// <summary>
/// End-to-end tests for the Inno Setup-built <c>ProteoWizard-Sharp-Setup.exe</c>.
/// Each test runs a silent install, verifies file deployment + registry state +
/// a quick CLI smoke (<c>msconvert.exe --help</c>), then silently
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
        "msconvert.exe",
        "seems-sharp.exe",
        "Pwiz.Vendor.Common.dll",
    };

    [TestMethod]
    public void Install_PerUser_DeploysAndConvertsVendorFile()
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
            AssertMsconvertSmokes(installDir);
        }
        finally
        {
            RunUninstaller(version, perMachine: false);
        }

        Assert.IsFalse(IsInstalled(version, perMachine: false),
            "Uninstall finished but the per-user uninstall registry key is still present.");
    }

    [TestMethod]
    public void Install_PerMachine_DeploysAndConvertsVendorFile()
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
            AssertMsconvertSmokes(installDir);
        }
        finally
        {
            RunUninstaller(version, perMachine: true);
        }

        Assert.IsFalse(IsInstalled(version, perMachine: true),
            "Uninstall finished but the per-machine uninstall registry key is still present.");
    }

    // ----------------- Helpers -----------------

    /// <summary>
    /// Try to locate the built Setup.exe. Search order: <c>PWIZ_INSTALLER_PATH</c>
    /// env var (absolute path), then the newest
    /// <c>pwiz-sharp/installer/build/ProteoWizard-Sharp-Setup-*.exe</c> (newest
    /// by file mtime, so the most recent build wins when older versioned
    /// installers are sitting alongside it). The NoNetRuntime variant is
    /// excluded — these tests target the bundled installer; the lightweight
    /// variant is the same payload minus the runtime and would just duplicate
    /// the coverage. Returns false + a human-readable reason if not found.
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

        string buildDir = PwizSharpPaths.InstallerBuildDir;
        if (Directory.Exists(buildDir))
        {
            // Versioned bundled-variant filename: ProteoWizard-Sharp-Setup-<ver>.exe
            // Exclude the NoNetRuntime variant by name; pick the newest by mtime so
            // a fresh build wins over older artifacts in the same folder.
            var candidates = new DirectoryInfo(buildDir)
                .GetFiles("ProteoWizard-Sharp-Setup-*.exe")
                .Where(f => !f.Name.Contains("NoNetRuntime", StringComparison.Ordinal))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToArray();
            if (candidates.Length > 0)
            {
                setupPath = candidates[0].FullName;
                reason = string.Empty;
                return true;
            }
        }

        setupPath = string.Empty;
        reason = $"No ProteoWizard-Sharp-Setup-*.exe found in {buildDir}. " +
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
    /// Returns the version the installer was built with. build.ps1 writes the
    /// resolved version (4.0.YYDOY-gitsha — the same value passed to ISCC as
    /// /DMyAppVersion=...) into <c>installer-version.txt</c> beside the .exe.
    /// We can't reliably parse it back from <c>Setup.iss</c> because the .iss
    /// only carries the local-debug fallback (`4.0.0-dev`); build.ps1's CLI
    /// override is what the installer actually registers under.
    /// </summary>
    private static string ReadInstallerVersion(string setupExePath)
    {
        string versionTxt = Path.Combine(
            Path.GetDirectoryName(setupExePath) ?? "",
            "installer-version.txt");
        if (File.Exists(versionTxt))
        {
            string v = File.ReadAllText(versionTxt).Trim();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        // Fallback: parse the Setup.iss fallback #define. Used only when the
        // test runs against an installer compiled directly via ISCC (no
        // build.ps1) — uncommon, but keeps local debugging painless.
        string iss = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(setupExePath) ?? "",
            "..",
            "Setup.iss"));
        Assert.IsTrue(File.Exists(iss),
            $"installer-version.txt missing at {versionTxt} and Setup.iss missing at {iss} — was build.ps1 run?");
        foreach (string line in File.ReadAllLines(iss))
        {
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#define ")) continue;
            if (!trimmed.Contains("MyAppVersion")) continue;
            int firstQuote = trimmed.IndexOf('"');
            int lastQuote = trimmed.LastIndexOf('"');
            if (firstQuote < 0 || lastQuote <= firstQuote) continue;
            return trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }
        throw new InvalidOperationException(
            $"Could not determine installer version from {versionTxt} or {iss}.");
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
    /// <c>/TASKS=""</c> disables every optional task — crucially the
    /// context-menu and Start-Menu / Desktop shortcut tasks. The context-menu
    /// verbs are SHARED across versions by design (last-installed-wins, no
    /// cleanup on uninstall), so enabling them in an automated test would
    /// leave orphan entries pointing at the deleted test install dir every
    /// run, breaking the user's real installation's right-click menu. Manual
    /// verification of those tasks is fine; the automated installer test
    /// covers everything else (file deployment, msconvert conversion smoke,
    /// uninstall accounting).
    /// </summary>
    private static void RunInstaller(string setupPath, bool allUsers)
    {
        string scope = allUsers ? "/ALLUSERS" : "/CURRENTUSER";
        var psi = new ProcessStartInfo(setupPath)
        {
            Arguments = $"{scope} /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=\"\"",
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
        string exe = Path.Combine(installDir, "msconvert.exe");
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
                $"msconvert.exe did not exit within 3 minutes. " +
                $"stdout=<{stdout}> stderr=<{stderr}>");
            Assert.AreEqual(0, proc.ExitCode,
                $"msconvert.exe exited with code {proc.ExitCode}. " +
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

        string candidate = PwizSharpPaths.CppVendorTestData("Thermo", fixtureName);
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
