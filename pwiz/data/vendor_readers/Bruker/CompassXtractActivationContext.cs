using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Pwiz.Vendor.Common;

// Compiled only when $(NativeVendorsAvailable) is true (Windows + vendor licenses) - see
// Bruker.csproj. The [SupportedOSPlatform] annotation stays for documentation.
#pragma warning disable CA1416

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Registration-free (side-by-side) activation of Bruker's CompassXtract COM server, which is
/// what the YEP and FID backends instantiate as <c>EDAL.MSAnalysis</c>.
/// </summary>
/// <remarks>
/// <para>pwiz C++ gets this for free: <c>msconvert.exe</c> embeds
/// <c>Interop.EDAL.SxS.manifest</c> as an assembly dependency in its own application manifest
/// (<c>pwiz_aux/msrc/utility/vendor_api/Bruker/Jamfile.jam:88-89</c>), so the process-wide
/// activation context Windows builds at startup already knows the CLSIDs. pwiz-sharp is a
/// <b>library</b> — msconvert-sharp, SeeMS and Skyline all host it — so it cannot rely on the
/// host EXE carrying a manifest. Instead we build the activation context programmatically with
/// <c>CreateActCtx</c> over the manifest that ships inside the vendor SDK archive, and activate
/// it around every call that touches COM.</para>
/// <para>Why "around every call" and not once: an activation context is activated on a
/// <b>thread's</b> activation stack, not process-wide. A reader opened on one thread and
/// consumed on another (msconvert's writer threads, Skyline's background loaders, SeeMS's STA UI
/// thread) would otherwise find no context on the consuming thread — which matters not only for
/// <c>CoCreateInstance</c> but for the <c>comInterfaceExternalProxyStub</c> entries the manifest
/// declares, which are what lets an EDAL interface pointer be marshalled across apartments.
/// <c>ActivateActCtx</c>/<c>DeactivateActCtx</c> are cheap user-mode stack pushes.</para>
/// <para>Apartment: we deliberately do <b>not</b> force STA. All CompassXtract coclasses are
/// <c>threadingModel="Both"</c> (see the manifest), .NET 8 threads are MTA by default, and pwiz
/// C++ has its <c>/CLRTHREADATTRIBUTE:STA</c> line commented out (Jamfile.jam:97) — i.e. it also
/// runs these MTA.</para>
/// <para>The handle is created once and never released: the CompassXtract DLLs stay loaded for
/// the life of the process anyway, and releasing the context while any RCW is still alive would
/// be a use-after-free.</para>
/// <para>Before the context is created, <see cref="StageVc90Runtime"/> mirrors the Visual C++
/// 2008 side-by-side assemblies the CompassXtract binaries depend on into the same directory, so
/// a machine without the VC++ 2008 redistributable can still load them.</para>
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class CompassXtractActivationContext
{
    /// <summary>Pin name in <c>build/vendor-sdk-pins.json</c> / <c>VendorSdkPins.generated.cs</c>.</summary>
    private const string PinName = "BrukerCompassXtract";

    /// <summary>
    /// Root manifest of the side-by-side assembly. It declares the EDAL coclasses (hosted by
    /// <c>CompassXtractMS.dll</c>) plus a dependency on <c>Interop.HSREADWRITELib.SxS</c>, which
    /// the loader resolves by probing the assembly directory for
    /// <c>Interop.HSREADWRITELib.SxS.manifest</c> — hence <c>HSReadWrite.dll</c> and its manifest
    /// have to be in the archive even though no pwiz code calls them.
    /// </summary>
    private const string ManifestFileName = "Interop.EDAL.SxS.manifest";

    /// <summary>Sibling manifest pulled in by the root manifest's &lt;dependency&gt; element.</summary>
    private const string DependentManifestFileName = "Interop.HSREADWRITELib.SxS.manifest";

    /// <summary>COM server DLL named by the root manifest's &lt;file&gt; element.</summary>
    private const string ComServerFileName = "CompassXtractMS.dll";

    /// <summary>
    /// Visual C++ 2008 side-by-side assemblies the CompassXtract binaries' own embedded manifests
    /// depend on. <c>CompassXtractMS.dll</c> names all three; the <c>BDal.*</c> DLLs it pulls in
    /// name CRT and, for <c>BDal.CCO.Transformation.dll</c>, OpenMP.
    /// </summary>
    private static readonly string[] Vc90Assemblies =
    {
        "Microsoft.VC90.CRT", "Microsoft.VC90.MFC", "Microsoft.VC90.OpenMP",
    };

    private const uint ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID = 0x004;

    private static readonly object Gate = new();
    private static IntPtr _handle;      // IntPtr.Zero until created; never released (see remarks)
    private static string? _sdkDirectory;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ActCtxW
    {
        public int CbSize;
        public uint DwFlags;
        public string? LpSource;
        public ushort WProcessorArchitecture;
        public ushort WLangId;
        public string? LpAssemblyDirectory;
        public string? LpResourceName;
        public string? LpApplicationName;
        public IntPtr HModule;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateActCtxW(ref ActCtxW pActCtx);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ActivateActCtx(IntPtr hActCtx, out IntPtr lpCookie);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeactivateActCtx(uint dwFlags, IntPtr ulCookie);

    /// <summary>
    /// Directory the CompassXtract SDK archive was extracted into. Downloading + unpacking
    /// happens on first access (<see cref="VendorSdkLoader.EnsureExtracted(string)"/>).
    /// </summary>
    /// <remarks>
    /// Nothing in that archive binds as a managed assembly or through <c>DllImport</c> — the COM
    /// server is reached through the activation context, not the CLR loader — so neither of
    /// <see cref="VendorSdkLoader"/>'s two resolvers ever fires for it and the pin has to be
    /// extracted explicitly. See the <c>$comment</c> on the <c>BrukerCompassXtract</c> entry in
    /// <c>build/vendor-sdk-pins.json</c>.
    /// </remarks>
    public static string SdkDirectory
    {
        get
        {
            EnsureCreated();
            return _sdkDirectory!;
        }
    }

    /// <summary>
    /// Activates the CompassXtract activation context on the calling thread. Dispose the result
    /// to pop it. Deactivation is strictly LIFO per thread, so keep the scope in a
    /// <c>using</c> and never let it span an <c>await</c> or a thread hop.
    /// </summary>
    public static IDisposable Activate()
    {
        EnsureCreated();
        if (!ActivateActCtx(_handle, out IntPtr cookie))
            throw new InvalidOperationException(
                "[CompassXtract] ActivateActCtx failed (Win32 error " +
                Marshal.GetLastPInvokeError() + "). CompassXtract COM classes cannot be resolved.");
        return new Scope(cookie);
    }

    private static void EnsureCreated()
    {
        if (_handle != IntPtr.Zero) return;
        lock (Gate)
        {
            if (_handle != IntPtr.Zero) return;

            string dir = VendorSdkLoader.EnsureExtracted(PinName);
            foreach (string required in new[] { ManifestFileName, DependentManifestFileName, ComServerFileName })
            {
                string path = Path.Combine(dir, required);
                if (!File.Exists(path))
                    throw new FileNotFoundException(
                        $"[CompassXtract] the {PinName} vendor SDK extracted to '{dir}' but does not " +
                        $"contain {required}; Bruker YEP / FID data cannot be read.", path);
            }

            StageVc90Runtime(dir);
            InstallPayloadBesideHost(dir);

            var ctx = new ActCtxW
            {
                // The context root is the SxS manifest itself, and lpAssemblyDirectory is the
                // probing root the loader uses for the private assemblies it names (the
                // Interop.HSREADWRITELib.SxS dependency) and for the <file> entries it redirects
                // (CompassXtractMS.dll, the Compressor_*/boost_*/mkl DLLs and the XML tables).
                DwFlags = ACTCTX_FLAG_ASSEMBLY_DIRECTORY_VALID,
                LpSource = Path.Combine(dir, ManifestFileName),
                LpAssemblyDirectory = dir,
            };
            ctx.CbSize = Marshal.SizeOf<ActCtxW>();

            IntPtr handle = CreateActCtxW(ref ctx);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                throw new InvalidOperationException(
                    $"[CompassXtract] CreateActCtx failed for '{ctx.LpSource}' " +
                    $"(Win32 error {Marshal.GetLastPInvokeError()}).");

            _sdkDirectory = dir;
            _handle = handle;
        }
    }

    /// <summary>
    /// Copies the CompassXtract payload out of the vendor cache and into the application
    /// directory, which is where Bruker's own object factory insists on finding it.
    /// </summary>
    /// <remarks>
    /// <para><b>Why this is necessary even though the activation context is correct.</b> The
    /// activation context resolves the three EDAL coclasses the manifest declares, and that part
    /// works from the cache. But <c>CompassXtractMS.dll</c> then instantiates its own internal
    /// objects through the BDal object registry (<c>BDal.CCO.Objects.xml</c> and friends), and
    /// those classes are not in any manifest — the first one it needs,
    /// <c>{6F53011E-F635-45CE-98B3-AA4B69A44247}</c>
    /// (<c>BDal.CCO.Transformation.CalibrationTransformatorFactorySerialization</c>), is not
    /// machine-registered either, which is what makes the whole stack registration-free in the
    /// first place. That factory locates its plugin DLLs relative to the <i>host executable's</i>
    /// directory. Measured: payload in the cache with the activation context active fails with
    /// <c>TypeNotInFactory</c>; so does adding the cache to the working directory or to
    /// <c>PATH</c>; copying the payload beside the executable is what makes
    /// <c>IMSAnalysis.Open</c> succeed.</para>
    /// <para><b>Why copy at run time rather than ship it.</b> 25 MB of 2008-era COM binaries that
    /// only YEP and FID data ever touch do not belong in every installer download; the payload is
    /// already fetched on demand, so this just extends the same lazy-fetch one directory further.
    /// A marker file named after the pinned SDK version records the install, so the second and
    /// every later run costs one <c>File.Exists</c>, and a re-pinned SDK re-copies under a new
    /// marker name rather than silently keeping stale binaries.</para>
    /// </remarks>
    private static void InstallPayloadBesideHost(string sdkDirectory)
    {
        string appDirectory = AppContext.BaseDirectory;

        // The extraction directory is named "<PinName>-<ShortSha>" by EnsureExtracted, so its
        // leaf doubles as the version stamp: a re-pinned SDK extracts elsewhere and therefore
        // looks for (and does not find) a differently-named marker.
        string stamp = Path.GetFileName(Path.TrimEndingDirectorySeparator(sdkDirectory));
        string marker = Path.Combine(appDirectory, stamp + ".installed");
        if (File.Exists(marker)) return;

        try
        {
            foreach (string source in Directory.GetFiles(sdkDirectory))
            {
                // ".ok" is VendorSdkLoader's own extraction marker, not part of the SDK.
                string name = Path.GetFileName(source);
                if (name.Equals(VendorSdkExtractionMarker, StringComparison.Ordinal)) continue;

                string destination = Path.Combine(appDirectory, name);
                if (FilesAreIdentical(source, destination)) continue;

                CopyThroughTemporary(source, destination, appDirectory);
            }

            File.WriteAllText(marker,
                $"CompassXtract runtime installed {DateTime.UtcNow:o}\nfrom {sdkDirectory}\n");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(ElevationRequiredMessage(appDirectory), ex);
        }
    }

    /// <summary>
    /// The one thing a user can actually do about an install directory they cannot write to.
    /// </summary>
    /// <remarks>
    /// Deliberately the exception's own message rather than a log line: under
    /// <c>%ProgramFiles%</c> this is the normal first-run outcome, and the alternative the user
    /// would otherwise be left with is an access-denied stack trace pointing at a path they have
    /// no reason to connect with Bruker data.
    /// </remarks>
    private static string ElevationRequiredMessage(string appDirectory) =>
        "Reading Bruker YEP / FID data needs Bruker's CompassXtract runtime installed alongside " +
        $"the application, and this account cannot write to \"{appDirectory}\". " +
        "Run one YEP or FID conversion with administrator permissions to install it there; " +
        "every run after that works normally, without elevation.";

    /// <summary>VendorSdkLoader's extraction-complete marker, which is not part of the payload.</summary>
    private const string VendorSdkExtractionMarker = ".ok";

    /// <summary>
    /// True when <paramref name="destination"/> already holds exactly the bytes of
    /// <paramref name="source"/>. Lets a run skip a DLL another process currently has loaded:
    /// re-copying it would fail on the sharing violation for no benefit.
    /// </summary>
    private static bool FilesAreIdentical(string source, string destination)
    {
        var destinationInfo = new FileInfo(destination);
        if (!destinationInfo.Exists) return false;
        var sourceInfo = new FileInfo(source);
        if (sourceInfo.Length != destinationInfo.Length) return false;

        // Only reached on the marker-miss path (first install, or an SDK re-pin), so the cost of
        // hashing ~25 MB is paid once per SDK version rather than per run.
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var destinationStream = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var a = new byte[64 * 1024];
        var b = new byte[64 * 1024];
        while (true)
        {
            int read = sourceStream.ReadAtLeast(a, a.Length, throwOnEndOfStream: false);
            int other = destinationStream.ReadAtLeast(b, b.Length, throwOnEndOfStream: false);
            if (read != other) return false;
            if (read == 0) return true;
            if (!a.AsSpan(0, read).SequenceEqual(b.AsSpan(0, read))) return false;
        }
    }

    /// <summary>
    /// Writes <paramref name="source"/> to a uniquely named temporary file in the destination
    /// directory and then moves it into place, so a second process installing concurrently either
    /// sees the old file or the complete new one, never a half-written one.
    /// </summary>
    private static void CopyThroughTemporary(string source, string destination, string appDirectory)
    {
        string temporary = Path.Combine(appDirectory, Path.GetRandomFileName() + ".cxttmp");
        try
        {
            File.Copy(source, temporary, overwrite: true);
            File.Move(temporary, destination, overwrite: true);
        }
        catch (IOException ex)
        {
            TryDelete(temporary);
            // Reached when the destination differs from the SDK copy AND is loaded by another
            // process - i.e. an SDK re-pin while an older msconvert/Skyline is still running.
            throw new IOException(
                $"could not replace \"{destination}\" with the CompassXtract runtime from " +
                $"\"{Path.GetDirectoryName(source)}\"; another process is most likely using the " +
                "older copy. Close other ProteoWizard / Skyline processes and retry.", ex);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* best effort */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }

    /// <summary>
    /// Mirrors the app-local Visual C++ 2008 runtime into <paramref name="assemblyDirectory"/> —
    /// the vendor cache directory this activation context declares as its assembly directory —
    /// so the CompassXtract binaries' VC90 dependencies can be satisfied privately instead of
    /// from a machine-wide VC++ 2008 redistributable.
    /// </summary>
    /// <remarks>
    /// <para><b>Layout.</b> A private side-by-side assembly is found at
    /// <c>&lt;probing root&gt;\&lt;name&gt;\&lt;name&gt;.manifest</c>, so each identity needs its own
    /// folder — three of them, even though Microsoft.VC90.OpenMP's two files ship inside the
    /// vendored Microsoft.VC90.CRT folder. Loose DLLs beside the executable, the way
    /// <c>Agilent.csproj</c> stages msvcr120/msvcp120, cannot work: those are resolved by plain
    /// name through the ordinary DLL search path, whereas a VC90 dependency is an assembly
    /// reference resolved through an activation context.</para>
    /// <para><b>Which placement resolves.</b> There are two, and they cover the two possible
    /// answers to "where does Windows root private-assembly probing for a DLL pulled in under an
    /// explicitly created activation context". This copy is the one expected to do the work: it
    /// lands in the directory passed as <c>lpAssemblyDirectory</c>, i.e. the probing root of the
    /// context that is active on this thread at the moment <c>CoCreateInstance</c> loads
    /// <c>CompassXtractMS.dll</c>. The app-local copy staged next to the host executable
    /// (Bruker.csproj) is the fallback for the other answer — the process's default activation
    /// context, rooted at the executable's directory, which is the layout Microsoft documents for
    /// local VC++ runtime deployment. Neither is redundant: the app-local copy is also the
    /// <i>source</i> for this one.</para>
    /// <para><b>When the source is missing</b> — a host that consumes Pwiz.Vendor.Bruker.dll
    /// without its content, say — this is a no-op and the machine-installed VC++ 2008
    /// redistributable remains the only route. That is exactly pwiz C++'s posture today, so the
    /// failure mode is no worse than the status quo; it is warned about rather than thrown on.</para>
    /// </remarks>
    private static void StageVc90Runtime(string assemblyDirectory)
    {
        foreach (string name in Vc90Assemblies)
        {
            string destinationDirectory = Path.Combine(assemblyDirectory, name);
            string destinationManifest = Path.Combine(destinationDirectory, name + ".manifest");
            if (File.Exists(destinationManifest)) continue;   // already staged by an earlier run

            string sourceDirectory = Path.Combine(AppContext.BaseDirectory, name);
            string sourceManifest = Path.Combine(sourceDirectory, name + ".manifest");
            if (!File.Exists(sourceManifest))
            {
                Trace.TraceWarning(
                    $"[CompassXtract] no app-local {name} beside {AppContext.BaseDirectory}; " +
                    "CompassXtract will need the Visual C++ 2008 redistributable installed.");
                continue;
            }

            try
            {
                Directory.CreateDirectory(destinationDirectory);
                // Payload first, manifest last: the manifest's presence is the "staged" marker
                // checked above, so writing it last keeps an interrupted copy from looking done.
                foreach (string file in Directory.GetFiles(sourceDirectory))
                {
                    if (string.Equals(file, sourceManifest, StringComparison.OrdinalIgnoreCase)) continue;
                    string destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
                    if (!File.Exists(destination)) File.Copy(file, destination);
                }
                File.Copy(sourceManifest, destinationManifest, overwrite: true);
            }
            catch (IOException ex)
            {
                Trace.TraceWarning($"[CompassXtract] could not stage {name} into '{destinationDirectory}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Trace.TraceWarning($"[CompassXtract] could not stage {name} into '{destinationDirectory}': {ex.Message}");
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        private IntPtr _cookie;
        private bool _popped;

        internal Scope(IntPtr cookie) => _cookie = cookie;

        public void Dispose()
        {
            if (_popped) return;
            _popped = true;
            DeactivateActCtx(0, _cookie);
            _cookie = IntPtr.Zero;
        }
    }
}
