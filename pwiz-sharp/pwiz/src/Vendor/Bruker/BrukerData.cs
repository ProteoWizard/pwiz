using Pwiz.Data.MsData.Readers;

namespace Pwiz.Vendor.Bruker;

/// <summary>
/// Bruker sub-format detection plus the static factory for <see cref="IBrukerData"/>. Port of
/// <c>Reader_Bruker_Detail::format</c> (<c>Reader_Bruker_Detail.cpp:39-130</c>) and the
/// <c>CompassData::create</c> factory (<c>CompassData.cpp:766-790</c>) — renamed from
/// "CompassData" because "Compass" is an old Bruker acquisition-software brand, unrelated to
/// TDF / TSF.
/// </summary>
public static class BrukerData
{
    /// <summary>
    /// Opens the <c>.d</c> directory at <paramref name="rawPath"/>: TDF (<c>analysis.tdf</c>),
    /// TSF (<c>analysis.tsf</c>) and BAF (<c>analysis.baf</c>) through Bruker's native SDKs,
    /// YEP (<c>analysis.yep</c>) and FID through CompassXtract.
    /// </summary>
    /// <param name="rawPath">The <c>.d</c> directory, or the root of a FID tree.</param>
    /// <param name="useRecalibratedState">
    /// Prefer recalibrated masses where the SDK offers them. Ignored on the CompassXtract path,
    /// which cpp also reads without recalibration.
    /// </param>
    public static IBrukerData Create(string rawPath, bool useRecalibratedState = true)
    {
        ArgumentNullException.ThrowIfNull(rawPath);
        if (!Directory.Exists(rawPath))
            throw new DirectoryNotFoundException($"Bruker .d directory not found: {rawPath}");

        string full = Path.GetFullPath(rawPath);
        return DetectFormat(full) switch
        {
            BrukerFormat.Tdf => new TdfData(full, useRecalibratedState),
            BrukerFormat.Tsf => new TsfData(full, useRecalibratedState),
            BrukerFormat.Baf => new Baf2SqlData(full, useRecalibratedState),
            BrukerFormat.Yep => CreateCompassXtract(full, BrukerFormat.Yep),
            BrukerFormat.Fid => CreateCompassXtract(full, BrukerFormat.Fid),
            _ => throw new NotSupportedException($"Bruker format at {rawPath} is not recognized."),
        };
    }

    /// <summary>
    /// YEP / FID go through Bruker's CompassXtract COM server, which is in-process COM and
    /// therefore Windows-only — the one Bruker sub-format family that cannot be read on Linux.
    /// </summary>
    // CA1859 wants the concrete CompassXtractData as the return type. It cannot be: that type
    // does not exist in a NO_COMPASSXTRACT build, where this method only throws.
#pragma warning disable CA1859
    private static IBrukerData CreateCompassXtract(string analysisDirectory, BrukerFormat format)
    {
#if NO_COMPASSXTRACT
        _ = analysisDirectory;
        _ = format;
        // VendorSupportNotEnabledException, not a plain NotSupportedException: this is the
        // "vendor SDK is not in this build" case, which VendorReaderTestHarness handles by
        // falling back to identify-only (ReadAndDiff catches exactly this subclass and
        // deliberately lets a plain NotSupportedException - "format not yet ported" - fail the
        // test). Throwing the plain type made the YEP and FID fixtures hard-fail the Linux CI
        // build, which cannot have CompassXtract by construction.
        throw new VendorSupportNotEnabledException(
            "Bruker YEP / FID data is read through CompassXtract, which is Windows-only COM. " +
            "This build of pwiz-sharp was produced without it (build on Windows with " +
            "-p:IAgreeToVendorLicenses=true to enable).");
#else
        // The OperatingSystem check is also what lets the platform analyzer see that the
        // Windows-only CompassXtractData is never constructed anywhere else.
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Bruker YEP / FID data is read through CompassXtract, which is Windows-only COM.");
        return new CompassXtractData(analysisDirectory, format);
#endif
    }
#pragma warning restore CA1859

    // ---------- format detection ----------

    /// <summary>
    /// Which Bruker sub-format lives at <paramref name="path"/> — a <c>.d</c> directory, a FID
    /// tree root, or a direct path to one of the metadata files inside one. Port of
    /// <c>Reader_Bruker_Detail::format</c>.
    /// </summary>
    /// <remarks>
    /// U2 is identified but not readable, matching cpp: its U2 reading path is commented out
    /// (<c>CompassData.cpp:530-538</c>) and Reader_Bruker_U2 is a Reader_Bruker_Dummy. cpp still
    /// NAMES it from Reader_Bruker::identify, which is what lets a file dialog show a U2 .d as a
    /// Bruker source; reporting Unknown here made it read as a plain folder.
    /// </remarks>
    public static BrukerFormat DetectFormat(string path)
    {
        if (string.IsNullOrEmpty(path)) return BrukerFormat.Unknown;

        // A path to a file inside the source maps to the enclosing format.
        if (File.Exists(path))
        {
            string leaf = Path.GetFileName(path).ToLowerInvariant();
            string parent = Path.GetDirectoryName(path) ?? string.Empty;
            return leaf switch
            {
                // cpp requires the absence of a sibling analysis.baf: a BAF acquisition can ship
                // a fid alongside it and must not be mistaken for FID data.
                "fid" when !HasBaf(parent) => BrukerFormat.Fid,
                "analysis.tdf" or "analysis.tdf_bin" => BrukerFormat.Tdf,
                "analysis.tsf" or "analysis.tsf_bin" => BrukerFormat.Tsf,
                "analysis.yep" => BrukerFormat.Yep,
                "analysis.baf" => BrukerFormat.Baf,
                _ when leaf.EndsWith(".u2", StringComparison.Ordinal) => BrukerFormat.U2,
                _ => BrukerFormat.Unknown,
            };
        }

        if (!Directory.Exists(path)) return BrukerFormat.Unknown;

        if (Exists(path, "analysis.tdf")) return BrukerFormat.Tdf;
        if (Exists(path, "analysis.tsf")) return BrukerFormat.Tsf;
        if (IsFidTree(path)) return BrukerFormat.Fid;
        if (Exists(path, "analysis.yep")) return BrukerFormat.Yep;
        if (Exists(path, "analysis.baf")) return BrukerFormat.Baf;
        // cpp Reader_Bruker_Detail.cpp:125-127: a U2 .d holds "<directory name minus .d>.u2".
        if (HasU2(path)) return BrukerFormat.U2;
        return BrukerFormat.Unknown;
    }

    /// <summary>
    /// Port of the FID branch of <c>Reader_Bruker_Detail::format</c>
    /// (<c>Reader_Bruker_Detail.cpp:84-104</c>), comment and all: "Every directory within the
    /// queried directory should have a <c>1/1SRef</c> subdirectory with a fid file in it, but we
    /// check only the first non-dotted directory for efficiency. This can fail, but those
    /// failures are acceptable."
    /// </summary>
    /// <remarks>
    /// Deliberately <i>not</i> a recursive fid search: <c>CsI_Pos_0_G1_000003.d</c> in pwiz's own
    /// test data is a BAF acquisition with a top-level <c>fid</c> beside <c>analysis.baf</c>, and
    /// only this shape — first child <i>directory</i>, with the analysis.baf guards — keeps it
    /// classified as BAF.
    /// </remarks>
    private static bool IsFidTree(string rootPath)
    {
        string[] children;
        try
        {
            children = Directory.GetDirectories(rootPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        Array.Sort(children, StringComparer.Ordinal);

        foreach (string child in children)
        {
            string name = Path.GetFileName(child);
            if (name.Length > 0 && name[0] == '.') continue;   // HACK: skip ".svn"

            foreach (string relative in FidProbePaths)
                if (File.Exists(Path.Combine(child, relative)))
                    return true;

            if (File.Exists(Path.Combine(child, "fid")) && !HasBaf(child)) return true;
            if (File.Exists(Path.Combine(rootPath, "fid")) && !HasBaf(rootPath)) return true;

            // Only the first non-dotted subdirectory is examined.
            return false;
        }
        return false;
    }

    private static readonly string[] FidProbePaths =
    {
        Path.Combine("1", "1SRef", "fid"), Path.Combine("1SRef", "fid"),
        Path.Combine("1", "1SLin", "fid"), Path.Combine("1SLin", "fid"),
        Path.Combine("1", "1Ref", "fid"),  Path.Combine("1Ref", "fid"),
        Path.Combine("1", "1Lin", "fid"),  Path.Combine("1Lin", "fid"),
    };

    /// <summary>Both spellings pwiz checks for; NTFS is case-insensitive but Linux is not.</summary>
    private static bool Exists(string directory, string lowercaseFileName) =>
        File.Exists(Path.Combine(directory, lowercaseFileName))
        || File.Exists(Path.Combine(directory, char.ToUpperInvariant(lowercaseFileName[0]) + lowercaseFileName[1..]));

    private static bool HasU2(string directory) =>
        File.Exists(Path.Combine(directory,
            Path.GetFileNameWithoutExtension(directory.TrimEnd(Path.DirectorySeparatorChar)) + ".u2"));

    private static bool HasBaf(string directory) =>
        directory.Length > 0 && Exists(directory, "analysis.baf");

    // ---------- FID tree enumeration ----------

    /// <summary>
    /// Every directory under <paramref name="rootPath"/> that directly contains a <c>fid</c>,
    /// in spectrum order. Port of <c>recursivelyEnumerateFIDs</c>
    /// (<c>SpectrumList_Bruker.cpp:555-566</c>), which pushes the <i>parent</i> of any entry
    /// named <c>fid</c> and otherwise recurses into directories.
    /// </summary>
    /// <remarks>
    /// Lives here rather than on the CompassXtract backend because <c>Reader_Bruker</c> needs the
    /// same list to build the <c>sourceFileList</c>, and it must not disappear from builds that
    /// compile the CompassXtract backend out. Entries are sorted ordinally so the spectrum order
    /// cannot depend on filesystem enumeration order; that ordering matches what boost's
    /// <c>directory_iterator</c> yields for the fixtures pwiz ships.
    /// </remarks>
    internal static List<string> EnumerateFidDirectories(string rootPath)
    {
        var result = new List<string>();
        if (!string.IsNullOrEmpty(rootPath))
            Recurse(rootPath, result);
        return result;

        static void Recurse(string path, List<string> into)
        {
            if (string.Equals(LeafName(path), "fid", StringComparison.Ordinal))
            {
                string? parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                    into.Add(parent);
                return;
            }

            if (!Directory.Exists(path)) return;
            string[] entries = Directory.GetFileSystemEntries(path);
            Array.Sort(entries, StringComparer.Ordinal);
            foreach (string entry in entries)
                Recurse(entry, into);
        }
    }

    /// <summary>
    /// The <c>sourceFile</c> id pwiz gives one fid: its path relative to
    /// <paramref name="rootPath"/>'s parent (so the root directory's own name is the first
    /// segment), with forward slashes. Port of <c>SpectrumList_Bruker.cpp:604-608</c>.
    /// </summary>
    /// <param name="rootPath">The directory that was handed to the reader.</param>
    /// <param name="fidDirectory">A directory returned by <see cref="EnumerateFidDirectories"/>.</param>
    internal static string FidRelativeId(string rootPath, string fidDirectory)
    {
        string fidPath = Path.Combine(fidDirectory, "fid");
        string? parent = Path.GetDirectoryName(TrimTrailingSeparators(rootPath));
        if (!string.IsNullOrEmpty(parent))
        {
            string prefix = TrimTrailingSeparators(parent) + Path.DirectorySeparatorChar;
            if (fidPath.StartsWith(prefix, StringComparison.Ordinal))
                fidPath = fidPath[prefix.Length..];
        }
        return fidPath.Replace('\\', '/');
    }

    private static string LeafName(string path) => Path.GetFileName(TrimTrailingSeparators(path));

    private static string TrimTrailingSeparators(string path) =>
        path.Length > 1
            ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;
}
