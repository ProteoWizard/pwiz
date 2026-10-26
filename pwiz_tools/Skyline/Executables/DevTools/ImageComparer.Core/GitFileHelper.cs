/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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

namespace ImageComparer.Core
{
    /// <summary>
    /// Git operations for reviewing changed screenshots. The Git index (staging area) records which
    /// changes have been accepted: accepting a screenshot stages it, so the changes still to review
    /// are the ones in the working tree that differ from the index. Until a file is accepted, its
    /// index version is the HEAD version.
    /// </summary>
    public static class GitFileHelper
    {
        /// <summary>
        /// Retrieves the binary content of a file in the Git index: the accepted version if the file
        /// has been accepted, and otherwise the committed (HEAD) version.
        /// </summary>
        /// <param name="fullPath">The fully qualified path of the file.</param>
        /// <returns>The binary content of the file in the index.</returns>
        public static byte[] GetGitFileBinaryContent(string fullPath)
        {
            return RunGitCommand(GetPathInfo(fullPath), "show :{RelativePath}", process =>
            {
                using (var memoryStream = new MemoryStream())
                {
                    process.StandardOutput.BaseStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            });
        }

        /// <summary>
        /// Tells if a path has uncommitted changes with respect to the Git HEAD.
        /// </summary>
        /// <param name="path">The fully qualified path to a file or directory.</param>
        /// <returns>True if the path has uncommitted changes, otherwise false.</returns>
        public static bool IsModified(string path)
        {
            var output = RunGitCommand(GetPathInfo(path), "status --porcelain \"{RelativePath}\"");

            // If there is any output the path has additions, deletions, or modifications
            return !string.IsNullOrWhiteSpace(output);
        }

        /// <summary>
        /// Gets the paths of modified files under a directory that still need review: files whose
        /// working tree content differs from the index. A file that has been accepted (staged) and
        /// not changed since is left out; one changed again after it was accepted is included.
        /// </summary>
        /// <param name="directoryPath">The fully qualified directory path.</param>
        /// <returns>An enumerable of file paths modified in the working tree.</returns>
        public static IEnumerable<string> GetChangedFilePaths(string directoryPath)
        {
            var output = RunGitCommand(GetPathInfo(directoryPath), "status --porcelain \"{RelativePath}\"");

            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // 'git status --porcelain' format: XY path/to/file, where X is the index status
                    // and Y the working tree status. " M" is a change not yet accepted, "MM" one changed
                    // again after it was accepted, and "M " an accepted change with nothing new.
                    if (line.Length < 4 || line[1] != 'M')
                        continue;
                    var filePath = line.Substring(3).Trim().Replace('/', Path.DirectorySeparatorChar);
                    yield return Path.Combine(GetPathInfo(directoryPath).Root, filePath);
                }
            }
        }

        /// <summary>
        /// Accepts a changed file by staging it in the Git index, so that it is included in the next
        /// commit and no longer listed by <see cref="GetChangedFilePaths"/>.
        /// </summary>
        /// <param name="fullPath">The fully qualified path of the file to accept.</param>
        public static void AcceptFile(string fullPath)
        {
            RunGitCommand(GetPathInfo(fullPath), "add -- \"{RelativePath}\"");
        }

        /// <summary>
        /// Reverts a file to its version in the Git index: the accepted version if the file has been
        /// accepted, and otherwise the committed (HEAD) version.
        /// </summary>
        /// <param name="fullPath">The fully qualified path of the file to revert.</param>
        public static void RevertFile(string fullPath)
        {
            RunGitCommand(GetPathInfo(fullPath), "checkout -- \"{RelativePath}\"");
        }

        /// <summary>
        /// Retrieves the root directory of the Git repository containing the given path.
        /// </summary>
        /// <param name="startPath">The directory or file path to start searching from.</param>
        /// <returns>The root directory of the Git repository, or null if not found.</returns>
        public static string GetGitRepositoryRoot(string startPath)
        {
            var currentDirectory = File.Exists(startPath)
                ? Path.GetDirectoryName(startPath)
                : startPath;

            while (!string.IsNullOrEmpty(currentDirectory))
            {
                // A regular checkout has a ".git" directory; a linked worktree has a ".git"
                // FILE containing a "gitdir:" pointer. Accept either as the repository root.
                var gitPath = Path.Combine(currentDirectory, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    return currentDirectory;
                }

                currentDirectory = Directory.GetParent(currentDirectory)?.FullName;
            }

            return null; // No Git repository found
        }

        private static T RunGitCommand<T>(PathInfo pathInfo, string commandTemplate, Func<Process, T> processOutput)
        {
            var command = commandTemplate.Replace("{RelativePath}", pathInfo.RelativePath);
            var processInfo = new ProcessStartInfo("git", command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true, // Prevent inheriting parent stdin (critical for MCP servers)
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = pathInfo.Root
            };

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.StandardInput.Close(); // Immediately close stdin so git doesn't block

                var output = processOutput(process);
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Git error: {error}");
                }

                return output;
            }
        }

        private static string RunGitCommand(PathInfo pathInfo, string commandTemplate)
        {
            return RunGitCommand(pathInfo, commandTemplate, process => process.StandardOutput.ReadToEnd());
        }

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
