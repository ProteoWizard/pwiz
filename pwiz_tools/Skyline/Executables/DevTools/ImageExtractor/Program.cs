/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Baml2006;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Size = System.Windows.Size;

namespace ImageExtractor
{
    /// <summary>
    /// Extracts icons from the Visual Studio ImageCatalog at one or more sizes.
    /// The catalog ships ready-made PNG bitmaps only at 16x16; every other size is
    /// rendered from the XAML vector image that Visual Studio itself uses at high DPI.
    /// </summary>
    internal class Program
    {
        private const string DLL_PATH = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\en\Microsoft.VisualStudio.ImageCatalog.resources.dll";
        private const string OUTPUT_SUBDIR = @"ai\.tmp\icons";

        // The only size the catalog stores as hand-tuned PNG bitmaps
        private const int NATIVE_PNG_SIZE = 16;
        private const string NATIVE_PNG_SUFFIX = ".16.16.png"; // Matches NATIVE_PNG_SIZE
        private const string PNG_PREFIX = "png/";
        private const string XAML_PREFIX = "xaml/";
        private const string BAML_EXT = ".baml";
        private const int MAX_SIZE = 1024;
        private const double SCREEN_DPI = 96;

        [STAThread] // WPF rendering of the vector images requires an STA thread
        private static int Main(string[] args)
        {
            if (!TryParseSizes(args, out var sizes))
            {
                PrintUsage();
                return 1;
            }

            // Find project root by looking for .git directory
            var projectRoot = FindProjectRoot();
            if (projectRoot == null)
            {
                Console.WriteLine("ERROR: Could not find project root (looking for .git directory)");
                return 1;
            }

            var outputRoot = Path.Combine(projectRoot, OUTPUT_SUBDIR);

            Console.WriteLine($"Project Root: {projectRoot}");
            Console.WriteLine($"DLL Path: {DLL_PATH}");
            Console.WriteLine($"Output Directory: {outputRoot}");
            Console.WriteLine($"Sizes: {string.Join(", ", sizes)}");
            Console.WriteLine();

            if (!File.Exists(DLL_PATH))
            {
                Console.WriteLine($"ERROR: DLL not found at {DLL_PATH}");
                Console.WriteLine("Make sure Visual Studio 2022 Community Edition is installed.");
                return 1;
            }

            foreach (var size in sizes)
                Directory.CreateDirectory(GetSizeDir(outputRoot, size));

            var extractNative = sizes.Contains(NATIVE_PNG_SIZE);
            var vectorSizes = sizes.Where(s => s != NATIVE_PNG_SIZE).ToList();
            var savedCounts = sizes.ToDictionary(s => s, _ => 0);

            Console.WriteLine("Loading assembly and extracting images directly from DLL...");
            Console.WriteLine();

            var assembly = Assembly.LoadFrom(DLL_PATH);
            var resourceNames = assembly.GetManifestResourceNames();
            Console.WriteLine($"Found {resourceNames.Length} manifest resource(s) in DLL");

            foreach (var manifestResourceName in resourceNames)
            {
                Console.WriteLine($"\nProcessing manifest resource: {manifestResourceName}");
                try
                {
                    using var stream = assembly.GetManifestResourceStream(manifestResourceName);
                    if (stream == null)
                    {
                        Console.WriteLine("  (stream is null, skipping)");
                        continue;
                    }

                    using var reader = new ResourceReader(stream);

                    foreach (DictionaryEntry entry in reader)
                    {
                        if (entry.Key is not string name)
                            continue;
                        var data = AsStream(entry.Value);
                        if (data == null)
                            continue;

                        if (extractNative && name.StartsWith(PNG_PREFIX) && name.EndsWith(NATIVE_PNG_SUFFIX))
                        {
                            SaveNativeBitmap(name, data, outputRoot, savedCounts);
                        }
                        else if (vectorSizes.Count > 0 && name.StartsWith(XAML_PREFIX) && name.EndsWith(BAML_EXT))
                        {
                            RenderVectorImage(name, data, vectorSizes, outputRoot, savedCounts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error processing resource: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done!");
            foreach (var size in sizes)
                Console.WriteLine($"  Saved {savedCounts[size]} {size}x{size} images to {GetSizeDir(outputRoot, size)}");
            return 0;
        }

        private static bool TryParseSizes(string[] args, out List<int> sizes)
        {
            sizes = new List<int>();
            if (args.Length == 0)
            {
                sizes.Add(NATIVE_PNG_SIZE);
                return true;
            }

            foreach (var arg in args)
            {
                if (!int.TryParse(arg, out var size) || size < 1 || size > MAX_SIZE)
                    return false;
                if (!sizes.Contains(size))
                    sizes.Add(size);
            }
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: ImageExtractor [size ...]");
            Console.WriteLine();
            Console.WriteLine($"  size   Square icon size in pixels, e.g. 16 24 32 48 (default: {NATIVE_PNG_SIZE})");
            Console.WriteLine($"         {NATIVE_PNG_SIZE} extracts the catalog's native PNG bitmaps; any other size");
            Console.WriteLine("         is rendered from the catalog's XAML vector images.");
            Console.WriteLine();
            Console.WriteLine("Example: dotnet run -c Release -- 16 32");
        }

        private static string? FindProjectRoot()
        {
            var currentDir = Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(currentDir);

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// Resource values arrive as a Stream (UnmanagedMemoryStream) or, for byte[]
        /// resources, as a byte array. Normalize both to a Stream.
        /// </summary>
        private static Stream? AsStream(object? value)
        {
            switch (value)
            {
                case Stream stream:
                    return stream;
                case byte[] bytes:
                    return new MemoryStream(bytes);
                default:
                    return null;
            }
        }

        private static void SaveNativeBitmap(string resourceName, Stream data, string outputRoot,
            IDictionary<int, int> savedCounts)
        {
            try
            {
                using var bitmap = new Bitmap(data);

                // Verify the bitmap really is the size its name claims before saving
                if (bitmap.Width != NATIVE_PNG_SIZE || bitmap.Height != NATIVE_PNG_SIZE)
                    return;

                var iconName = GetIconName(resourceName, PNG_PREFIX, NATIVE_PNG_SUFFIX);
                var outputPath = GetOutputPath(outputRoot, iconName, NATIVE_PNG_SIZE);
                bitmap.Save(outputPath, ImageFormat.Png);
                Console.WriteLine($"  Saved: {Path.GetFileName(outputPath)}");
                savedCounts[NATIVE_PNG_SIZE]++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error converting {resourceName}: {ex.Message}");
            }
        }

        private static void RenderVectorImage(string resourceName, Stream data, IEnumerable<int> sizes,
            string outputRoot, IDictionary<int, int> savedCounts)
        {
            try
            {
                var root = XamlReader.Load(new Baml2006Reader(data));
                var iconName = GetIconName(resourceName, XAML_PREFIX, BAML_EXT);

                foreach (var size in sizes)
                {
                    var visual = CreateVisual(root, size);
                    if (visual == null)
                    {
                        Console.WriteLine($"  Skipped {resourceName}: unsupported XAML root {root.GetType().Name}");
                        return;
                    }

                    var bitmap = new RenderTargetBitmap(size, size, SCREEN_DPI, SCREEN_DPI, PixelFormats.Pbgra32);
                    bitmap.Render(visual);

                    // A FrameworkElement can only have one parent, so release it for the next size
                    if (visual is Viewbox viewbox)
                        viewbox.Child = null;

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var outputPath = GetOutputPath(outputRoot, iconName, size);
                    using (var fileStream = File.Create(outputPath))
                    {
                        encoder.Save(fileStream);
                    }
                    Console.WriteLine($"  Saved: {Path.GetFileName(outputPath)}");
                    savedCounts[size]++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error rendering {resourceName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Wraps the loaded XAML root in a visual that fills a size x size square.
        /// The catalog uses Viewbox roots, but Drawing, Brush and ImageSource roots
        /// are handled too. Returns null for anything else.
        /// </summary>
        private static Visual? CreateVisual(object root, int size)
        {
            var bounds = new Rect(0, 0, size, size);

            if (root is FrameworkElement element)
            {
                var viewbox = new Viewbox
                {
                    Child = element,
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Uniform
                };
                viewbox.Measure(new Size(size, size));
                viewbox.Arrange(bounds);
                viewbox.UpdateLayout();
                return viewbox;
            }

            Brush? brush = root switch
            {
                Brush rootBrush => rootBrush,
                Drawing drawing => new DrawingBrush(drawing) { Stretch = Stretch.Uniform },
                ImageSource image => new ImageBrush(image) { Stretch = Stretch.Uniform },
                _ => null
            };
            if (brush == null)
                return null;

            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                context.DrawRectangle(brush, null, bounds);
            }
            return drawingVisual;
        }

        /// <summary>
        /// Turns a resource name like "png/database.16.16.png" or "xaml/database.baml"
        /// into the bare icon name "database", flattening any remaining path separators.
        /// </summary>
        private static string GetIconName(string resourceName, string prefix, string suffix)
        {
            var name = resourceName.Substring(prefix.Length, resourceName.Length - prefix.Length - suffix.Length);
            return name.Replace("/", "_");
        }

        private static string GetSizeDir(string outputRoot, int size)
        {
            return Path.Combine(outputRoot, $"{size}x{size}");
        }

        private static string GetOutputPath(string outputRoot, string iconName, int size)
        {
            return Path.Combine(GetSizeDir(outputRoot, size), $"{iconName}.{size}.{size}.png");
        }
    }
}
