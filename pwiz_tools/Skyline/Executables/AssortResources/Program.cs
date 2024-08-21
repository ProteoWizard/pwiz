using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;


//
// Script for moving single-use resources out of the traditional and overcrowded Skyline\Properties\Resources.resx file into per-dialog resource files
//
// Typical command args "--projectfile Skyline.csproj --resourcefile Properties\Resources.resx" with working directory "pwiz_tools\Skyline"
//
// Optional arg "--inspectonly true" won't actually change anything, it just lets you know that changes could be made
//

namespace AssortResources
{
    internal class Program
    {
        public class Options
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            [Option(Required = true)]
            public string ProjectFile { get; set; }
            [Option(Required = true)]
            public string ResourceFile { get; set; }

            [Option(Required = false)]
            public bool InspectOnly { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunOptions);

        }

        static void RunOptions(Options options)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectFile = Path.Combine(currentDirectory, options.ProjectFile);
            if (!File.Exists(projectFile))
            {
                Console.Error.WriteLine("Project file {0} does not exist", projectFile);
                return;
            }

            var resourceFile = Path.Combine(currentDirectory, options.ResourceFile);
            if (!File.Exists(resourceFile))
            {
                Console.Error.WriteLine("Resource file {0} does not exist", resourceFile);
                return;
            }

            var inspectOnly = options.InspectOnly;

            // Look for any .csproj in the immediate subfolder of the main project
            // Strings which are referenced by these other .csproj files will not get moved
            var otherProjectPaths = new List<string>();
            // ReSharper disable once AssignNullToNotNullAttribute
            foreach (var subfolder in Directory.GetDirectories(Path.GetDirectoryName(projectFile)))
            {
                var otherProjectPath = Path.Combine(subfolder, Path.GetFileNameWithoutExtension(subfolder) + ".csproj");
                if (File.Exists(otherProjectPath))
                {
                    otherProjectPaths.Add(otherProjectPath);
                }
            }
            var resourceAssorter = new ResourceAssorter(projectFile, resourceFile, inspectOnly, otherProjectPaths.ToArray());
            resourceAssorter.DoWork();
        }
    }
}
