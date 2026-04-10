using System;
using System.IO;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Spectral library file format.
    /// Maps to osprey-core/src/config.rs LibrarySource variants.
    /// </summary>
    public enum LibraryFormat
    {
        DiannTsv,
        Blib,
        Elib,
        SkylineDocument
    }

    /// <summary>
    /// Spectral library source, combining format and file path.
    /// Maps to osprey-core/src/config.rs LibrarySource.
    /// </summary>
    public class LibrarySource
    {
        /// <summary>The detected or specified library format.</summary>
        public LibraryFormat Format { get; }

        /// <summary>Path to the library file.</summary>
        public string Path { get; }

        public LibrarySource(LibraryFormat format, string path)
        {
            Format = format;
            Path = path;
        }

        /// <summary>
        /// Detect library format from file extension.
        /// .blib -> Blib, .elib -> Elib, .sky -> SkylineDocument, default -> DiannTsv.
        /// </summary>
        public static LibrarySource FromPath(string path)
        {
            string ext = (System.IO.Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".blib":
                    return new LibrarySource(LibraryFormat.Blib, path);
                case ".elib":
                    return new LibrarySource(LibraryFormat.Elib, path);
                case ".sky":
                    return new LibrarySource(LibraryFormat.SkylineDocument, path);
                default:
                    return new LibrarySource(LibraryFormat.DiannTsv, path);
            }
        }
    }
}
