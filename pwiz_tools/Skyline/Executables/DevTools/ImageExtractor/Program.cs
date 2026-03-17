using System.Drawing;
using System.Drawing.Imaging;
using System.Resources;
using System.Reflection;

namespace ImageExtractor
{
    internal class Program
    {
        static int Main()
        {
            // Find project root by looking for .git directory
            var projectRoot = FindProjectRoot();
            if (projectRoot == null)
            {
                Console.WriteLine("ERROR: Could not find project root (looking for .git directory)");
                return 1;
            }

            Console.WriteLine($"Project Root: {projectRoot}");

            var dllPath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\en\Microsoft.VisualStudio.ImageCatalog.resources.dll";
            var outputDir = Path.Combine(projectRoot, @"ai\.tmp\icons");

            Console.WriteLine($"DLL Path: {dllPath}");
            Console.WriteLine($"Output Directory: {outputDir}");
            Console.WriteLine();

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"ERROR: DLL not found at {dllPath}");
                Console.WriteLine("Make sure Visual Studio 2022 Community Edition is installed.");
                return 1;
            }

            Directory.CreateDirectory(outputDir);

            Console.WriteLine("Loading assembly and extracting 16x16 PNGs directly from DLL...");
            Console.WriteLine();

            // Load the assembly and extract resources directly
            var assembly = Assembly.LoadFrom(dllPath);
            var resourceNames = assembly.GetManifestResourceNames();
            Console.WriteLine($"Found {resourceNames.Length} manifest resource(s) in DLL");

            int savedCount = 0;

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

                    foreach (System.Collections.DictionaryEntry entry in reader)
                    {
                        var name = entry.Key as string;
                        if (name == null) continue;

                        // Only process resources that look like 16x16 PNG files
                        if (!name.Contains("16.16.png"))
                            continue;

                        // Extract the PNG data
                        var value = entry.Value;

                        // The value might be a byte array (Stream) that we need to convert to a bitmap
                        if (value is byte[] bytes)
                        {
                            try
                            {
                                using var ms = new MemoryStream(bytes);
                                using var bitmap = new Bitmap(ms);

                                // Verify it's actually 16x16 before saving
                                if (bitmap.Width == 16 && bitmap.Height == 16)
                                {
                                    // Extract just the filename from the resource name (remove "png/" prefix)
                                    var fileName = name.Replace("png/", "").Replace("/", "_");
                                    var outputPath = Path.Combine(outputDir, fileName);
                                    bitmap.Save(outputPath, ImageFormat.Png);
                                    Console.WriteLine($"  Saved: {fileName}");
                                    savedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error converting {name}: {ex.Message}");
                            }
                        }
                        else if (value is Stream valueStream)
                        {
                            try
                            {
                                using var bitmap = new Bitmap(valueStream);

                                if (bitmap.Width == 16 && bitmap.Height == 16)
                                {
                                    var fileName = name.Replace("png/", "").Replace("/", "_");
                                    var outputPath = Path.Combine(outputDir, fileName);
                                    bitmap.Save(outputPath, ImageFormat.Png);
                                    Console.WriteLine($"  Saved: {fileName}");
                                    savedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error converting {name}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error processing resource: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Done! Saved {savedCount} 16x16 images to {outputDir}");
            return 0;
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
    }
}
