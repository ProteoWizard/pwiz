using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;

namespace AssortResources
{
    internal class Program
    {
        public class Options
        {
            [Option(Required = true)]
            public string ProjectFile { get; set; }
            [Option(Required = true)]
            public string ResourceFile { get; set; }
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

            var resourceAssorter = new ResourceAssorter(Path.GetDirectoryName(projectFile), projectFile, resourceFile);
            resourceAssorter.DoWork();
        }
    }
}
