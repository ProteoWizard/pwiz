/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ImageComparer
{
    internal static class GitFileHelper
    {
        /// <summary>
        /// Retrieves the committed binary content of a file in a Git repository.
        /// </summary>
        /// <param name="fullPath">The fully qualified path of the file.</param>
        /// <returns>The committed binary content of the file.</returns>
        public static byte[] GetGitFileBinaryContent(string fullPath)
        {
            return RunGitCommand(GetPathInfo(fullPath), "show HEAD:{RelativePath}", process =>
            {
                using var memoryStream = new MemoryStream();
                process.StandardOutput.BaseStream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            });
        }

        /// <summary>
        /// Gets a list of changed file paths under a specific directory.
        /// </summary>
        /// <param name="directoryPath">The fully qualified directory path.</param>
        /// <returns>An enumerable of file paths that have been modified, added, or deleted.</returns>
        public static IEnumerable<string> GetChangedFilePaths(string directoryPath)
        {
            var output = RunGitCommand(GetPathInfo(directoryPath), "status --porcelain \"{RelativePath}\"");

            using var reader = new StringReader(output);
            while (reader.ReadLine() is { } line)
            {
                if (!line.StartsWith(" M "))
                    continue;
                // 'git status --porcelain' format: XY path/to/file
                var filePath = line.Substring(3).Replace('/', Path.DirectorySeparatorChar);
                yield return Path.Combine(GetPathInfo(directoryPath).Root, filePath);
            }
        }

        /// <summary>
        /// Reverts a file to its state in the HEAD commit.
        /// </summary>
        /// <param name="fullPath">The fully qualified path of the file to revert.</param>
        public static void RevertFileToHead(string fullPath)
        {
            RunGitCommand(GetPathInfo(fullPath), "checkout HEAD -- \"{RelativePath}\"");
        }

        /// <summary>
        /// Executes a Git command in the specified repository and returns the standard output as a string.
        /// </summary>
        /// <param name="pathInfo">The PathInfo object for the target path.</param>
        /// <param name="commandTemplate">The Git command template with placeholders (e.g., {RelativePath}).</param>
        /// <param name="processOutput">A function that takes a running process and returns the necessary output</param>
        /// <returns>Output of type T from the process.</returns>
        private static T RunGitCommand<T>(PathInfo pathInfo, string commandTemplate, Func<Process, T> processOutput)
        {
            var command = commandTemplate.Replace("{RelativePath}", pathInfo.RelativePath);
            var processInfo = new ProcessStartInfo("git", command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = pathInfo.Root
            };

            using var process = new Process();
            process.StartInfo = processInfo;
            process.Start();

            var output = processOutput(process);
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Git error: {error}");
            }

            return output;
        }

        private static string RunGitCommand(PathInfo pathInfo, string commandTemplate)
        {
            return RunGitCommand(pathInfo, commandTemplate, process => process.StandardOutput.ReadToEnd());
        }

        /// <summary>
        /// Retrieves the root directory of the Git repository containing the given directory.
        /// </summary>
        /// <param name="startPath">The directory or file path to start searching from.</param>
        /// <returns>The root directory of the Git repository, or null if not found.</returns>
        private static string GetGitRepositoryRoot(string startPath)
        {
            var currentDirectory = File.Exists(startPath)
                ? Path.GetDirectoryName(startPath)
                : startPath;

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                if (Directory.Exists(Path.Combine(currentDirectory, ".git")))
                {
                    return currentDirectory;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            return null; // No Git repository found
        }

        /// <summary>
        /// Retrieves path information for a given file or directory, including the repository root and relative path.
        /// </summary>
        /// <param name="fullPath">The fully qualified file or directory path.</param>
        /// <returns>A PathInfo object containing the repository root and the relative path.</returns>
        private static PathInfo GetPathInfo(string fullPath)
        {
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                throw new FileNotFoundException($"The path '{fullPath}' does not exist.");
            }

            var repoRoot = GetGitRepositoryRoot(fullPath);
            if (repoRoot == null)
            {
                throw new InvalidOperationException($"The path '{fullPath}' is not part of a Git repository.");
            }

            var relativePath = GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
            return new PathInfo { Root = repoRoot, RelativePath = relativePath };
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
            if (fullPath.ToLowerInvariant().StartsWith(basePath.ToLowerInvariant()))
            {
                return fullPath.Substring(basePath.Length);
            }
            return fullPath;
        }

        private struct PathInfo
        {
            public string Root { get; set; }
            public string RelativePath { get; set; }
        }
    }

}