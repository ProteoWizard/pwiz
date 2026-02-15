/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.Common.CommonResources;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Common.SystemUtil
{
    public static class FileEx
    {
        public static bool IsDirectory(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory;
        }

        public static bool IsFile(string path)
        {
            return !IsDirectory(path);
        }

        public static bool IsWritable(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                // An IOException means the file is locked
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Or we lack permissions can also make it not possible to write to
                return false;
            }
        }

        public static bool AreIdenticalFiles(string pathA, string pathB)
        {
            var infoA = new FileInfo(pathA);
            var infoB = new FileInfo(pathB);
            if (infoA.Length != infoB.Length)
                return false;
            // Credit from here to https://stackoverflow.com/questions/968935/compare-binary-files-in-c-sharp
            using (var s1 = new FileStream(pathA, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var s2 = new FileStream(pathB, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var b1 = new BinaryReader(s1))
            using (var b2 = new BinaryReader(s2))
            {
                while (true)
                {
                    var data1 = b1.ReadBytes(64 * 1024);
                    var data2 = b2.ReadBytes(64 * 1024);
                    if (data1.Length != data2.Length)
                        return false;
                    if (data1.Length == 0)
                        return true;
                    if (!data1.SequenceEqual(data2))
                        return false;
                }
            }
        }

        public static void SafeDelete(string path, bool ignoreExceptions = false)
        {
            var hint = $@"File.Delete({path})";
            if (ignoreExceptions)
            {
                try
                {
                    if (path != null && File.Exists(path))
                        TryHelper.TryTwice(() => File.Delete(path), hint);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }

                return;
            }

            try
            {
                TryHelper.TryTwice(() => File.Delete(path), hint);
            }
            catch (ArgumentException e)
            {
                if (path == null || string.IsNullOrEmpty(path.Trim()))
                    throw new DeleteException(MessageResources.FileEx_SafeDelete_Path_is_empty, e);
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Path_contains_invalid_characters___0_, path), e);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Directory_could_not_be_found___0_, path), e);
            }
            catch (NotSupportedException e)
            {
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_File_path_is_invalid___0_, path), e);
            }
            catch (PathTooLongException e)
            {
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_File_path_is_too_long___0_, path), e);
            }
            catch (IOException e)
            {
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Unable_to_delete_file_which_is_in_use___0_, path), e);
            }
            catch (UnauthorizedAccessException e)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.IsReadOnly)
                    throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Unable_to_delete_read_only_file___0_, path), e);
                if (Directory.Exists(path))
                    throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Unable_to_delete_directory___0_, path), e);
                throw new DeleteException(string.Format(MessageResources.FileEx_SafeDelete_Insufficient_permission_to_delete_file___0_, path), e);
            }
        }

        public class DeleteException : IOException
        {
            public DeleteException(string message, Exception innerException)
                : base(message, innerException)
            {
            }
        }

        public static string GetElapsedTimeExplanation(DateTime startTime, DateTime endTime)
        {
            long deltaTicks = endTime.Ticks - startTime.Ticks;
            var elapsedSpan = new TimeSpan(deltaTicks);
            if (elapsedSpan.TotalMinutes > 0)
                return string.Format(@"{0} minutes, {1} seconds", elapsedSpan.TotalMinutes, elapsedSpan.Seconds);
            if (elapsedSpan.TotalSeconds > 0)
                return elapsedSpan.TotalSeconds + @" seconds";
            if (elapsedSpan.TotalMilliseconds > 0)
                return elapsedSpan.TotalMilliseconds + @" milliseconds";
            return deltaTicks + @" ticks";
        }

        /// <summary>
        /// Tries to create a hard-link from sourceFilepath to destinationFilepath and returns true if the link was successfully created.
        /// </summary>
        public static bool CreateHardLink(string sourceFilepath, string destinationFilepath)
        {
            return Kernel32.CreateHardLink(destinationFilepath, sourceFilepath, IntPtr.Zero);
        }

        /// <summary>
        /// Tries to create a hard-link from sourceFilepath to destinationFilepath and if that fails, it copies the file instead.
        /// </summary>
        public static void HardLinkOrCopyFile(string sourceFilepath, string destinationFilepath, bool overwrite = false)
        {
            Directory.CreateDirectory(PathEx.GetDirectoryName(destinationFilepath));
            if (!CreateHardLink(sourceFilepath, destinationFilepath))
                File.Copy(sourceFilepath, destinationFilepath, overwrite);
        }
    }
}