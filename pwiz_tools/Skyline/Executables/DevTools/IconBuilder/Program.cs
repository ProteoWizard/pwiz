/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Windows.Size;

namespace IconBuilder
{
    /// <summary>
    /// Produces the per-DPI PNG set for every Skyline icon listed in icons.manifest.
    /// An icon's source is either a vector drawing (Icons/Vector/Name.xaml, a WPF Canvas
    /// rendered at each size) or a raster (Icons/Raster/Name.W.H.png, a 2x upscale of the
    /// native bitmap copied at its own size and bicubic-scaled to the others). Either way
    /// the output is Name.Size.png per requested size, so consumers never see the difference.
    /// </summary>
    internal class Program
    {
        private const string MANIFEST_NAME = "icons.manifest";
        private const string VECTOR_DIR = @"Icons\Vector";
        private const string RASTER_DIR = @"Icons\Raster";
        private const string DEFAULT_OUT_DIR = "Output";
        private const double SCREEN_DPI = 96;

        [STAThread] // WPF rendering requires an STA thread
        private static int Main(string[] args)
        {
            if (!TryParseArgs(args, out var manifestPath, out var outDir, out var names))
            {
                PrintUsage();
                return 1;
            }

            manifestPath ??= FindManifest();
            if (manifestPath == null || !File.Exists(manifestPath))
            {
                Console.WriteLine($"ERROR: {MANIFEST_NAME} not found. Run from the IconBuilder folder or pass --manifest <path>.");
                return 1;
            }

            var projectDir = Path.GetDirectoryName(manifestPath)!;
            outDir ??= Path.Combine(projectDir, DEFAULT_OUT_DIR);
            Directory.CreateDirectory(outDir);

            var entries = ReadManifest(manifestPath);
            if (names.Count > 0)
                entries = entries.Where(e => names.Contains(e.Name, StringComparer.OrdinalIgnoreCase)).ToList();

            Console.WriteLine($"Manifest: {manifestPath}");
            Console.WriteLine($"Output:   {outDir}");
            Console.WriteLine($"Icons:    {entries.Count}");
            Console.WriteLine();

            int written = 0, failed = 0;
            foreach (var entry in entries)
            {
                try
                {
                    var count = entry.Kind == SourceKind.vector
                        ? RenderVector(Path.Combine(projectDir, VECTOR_DIR, entry.Name + ".xaml"), entry, outDir)
                        : ScaleRaster(FindRaster(Path.Combine(projectDir, RASTER_DIR), entry.Name), entry, outDir);
                    written += count;
                    Console.WriteLine($"  {entry.Name,-26} {entry.Kind,-6} {string.Join(", ", entry.Sizes)}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"  {entry.Name,-26} ERROR: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Done: {written} files written, {failed} icon(s) failed.");
            return failed == 0 ? 0 : 1;
        }

        private static bool TryParseArgs(string[] args, out string? manifestPath, out string? outDir, out List<string> names)
        {
            manifestPath = null;
            outDir = null;
            names = new List<string>();
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--manifest" when i + 1 < args.Length:
                        manifestPath = args[++i];
                        break;
                    case "--out" when i + 1 < args.Length:
                        outDir = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        return false;
                    default:
                        if (args[i].StartsWith("-"))
                            return false;
                        names.Add(args[i]);
                        break;
                }
            }
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: IconBuilder [--manifest <path>] [--out <dir>] [IconName ...]");
            Console.WriteLine();
            Console.WriteLine("  --manifest  Path to icons.manifest (default: found by walking up from the exe or cwd)");
            Console.WriteLine("  --out       Output folder for <Name>.<Size>.png files (default: Output next to the manifest)");
            Console.WriteLine("  IconName    Build only these icons (default: every icon in the manifest)");
            Console.WriteLine();
            Console.WriteLine("Example: dotnet run -c Release -- Save Copy Paste");
        }

        private static string? FindManifest()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, MANIFEST_NAME);
                    if (File.Exists(candidate))
                        return candidate;
                    dir = dir.Parent;
                }
            }
            return null;
        }

        /// <summary>
        /// Manifest lines are "Name kind sizes", whitespace separated, with kind "vector" or
        /// "raster" and sizes a comma list of square pixel sizes. Blank lines and lines starting
        /// with # are ignored.
        /// </summary>
        private static List<IconEntry> ReadManifest(string path)
        {
            var entries = new List<IconEntry>();
            var lineNumber = 0;
            foreach (var rawLine in File.ReadLines(path))
            {
                lineNumber++;
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3 || !Enum.TryParse<SourceKind>(parts[1], out var kind))
                    throw new FormatException($"{MANIFEST_NAME} line {lineNumber}: expected 'Name vector|raster sizes', got '{line}'");
                var sizes = parts[2].Split(',').Select(s => int.Parse(s.Trim())).ToArray();
                entries.Add(new IconEntry(parts[0], kind, sizes));
            }
            return entries;
        }

        private static int RenderVector(string xamlPath, IconEntry entry, string outDir)
        {
            if (!File.Exists(xamlPath))
                throw new FileNotFoundException("Vector source not found", xamlPath);

            FrameworkElement root;
            using (var stream = File.OpenRead(xamlPath))
            {
                root = (FrameworkElement)XamlReader.Load(stream);
            }

            foreach (var size in entry.Sizes)
            {
                var viewbox = new Viewbox
                {
                    Child = root,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform
                };
                viewbox.Measure(new Size(size, size));
                viewbox.Arrange(new Rect(0, 0, size, size));
                viewbox.UpdateLayout();

                var bitmap = new RenderTargetBitmap(size, size, SCREEN_DPI, SCREEN_DPI, PixelFormats.Pbgra32);
                bitmap.Render(viewbox);
                // A FrameworkElement can only have one parent, so release it for the next size
                viewbox.Child = null;

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var fileStream = File.Create(GetOutputPath(outDir, entry.Name, size)))
                {
                    encoder.Save(fileStream);
                }
            }
            return entry.Sizes.Length;
        }

        private static string FindRaster(string rasterDir, string name)
        {
            // Raster sources are named Name.W.H.png; the size is read from the image itself.
            var matches = Directory.Exists(rasterDir)
                ? Directory.GetFiles(rasterDir, name + ".*.png")
                : Array.Empty<string>();
            if (matches.Length != 1)
                throw new FileNotFoundException($"Expected exactly one raster source {name}.W.H.png, found {matches.Length}", rasterDir);
            return matches[0];
        }

        /// <summary>
        /// Copies the raster at its own size and scales it to the others with the same
        /// high-quality bicubic resampling Skyline's DpiUtil uses at run time.
        /// </summary>
        private static int ScaleRaster(string pngPath, IconEntry entry, string outDir)
        {
            using var source = new Bitmap(pngPath);
            foreach (var size in entry.Sizes)
            {
                var outputPath = GetOutputPath(outDir, entry.Name, size);
                if (source.Width == size && source.Height == size)
                {
                    File.Copy(pngPath, outputPath, true);
                    continue;
                }
                if (size > source.Width)
                    Console.WriteLine($"  WARNING: {entry.Name} scaled up from {source.Width} to {size}");

                using var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                scaled.SetResolution((float)SCREEN_DPI, (float)SCREEN_DPI);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, new Rectangle(0, 0, size, size));
                }
                scaled.Save(outputPath, ImageFormat.Png);
            }
            return entry.Sizes.Length;
        }

        private static string GetOutputPath(string outDir, string name, int size)
        {
            return Path.Combine(outDir, $"{name}.{size}.png");
        }

        private enum SourceKind
        {
            vector,
            raster
        }

        private record IconEntry(string Name, SourceKind Kind, int[] Sizes);
    }
}
