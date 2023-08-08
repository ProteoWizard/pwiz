using SharedBatch.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SharedBatch
{
    public sealed class FileSaver : IDisposable
    {
        private const string TEMP_PREFIX = "~SB";
        public string SafeName { get; private set; }
        public string RealName { get; private set; }

        /// <summary>
        /// Construct an instance of <see cref="FileSaver"/> to manage saving to a temporary
        /// file, and then renaming to the final destination.
        /// </summary>
        /// <param name="fileName">File path to the final destination</param>
        public FileSaver(string fileName)
        {
            RealName = fileName;

            string dirName = Path.GetDirectoryName(fileName);
            string tempName = GetTempFileName(dirName, TEMP_PREFIX);

            if (!Equals(dirName, tempName))
                SafeName = tempName;
        }

        public void Commit()
        {
            if (!File.Exists(SafeName)) return;

            try
            {
                File.Move(SafeName, RealName);
                Dispose();
            }
            catch (Exception e)
            {
                Trace.TraceWarning(@"Exception in FileSaver.Commit: {0}", e);
            }
        }

        public void Dispose()
        {
            if (!File.Exists(SafeName)) return;
            File.Delete(SafeName);
            SafeName = null;
        }

        private string GetTempFileName(string basePath, string prefix)
        {
            return GetTempFileName(basePath, prefix, 0);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetTempFileName(string lpPathName, string lpPrefixString,
            uint uUnique, [Out] StringBuilder lpTempFileName);

        private static string GetTempFileName(string basePath, string prefix, uint unique)
        {
            // 260 is MAX_PATH in Win32 windows.h header
            // 'sb' needs >0 size else GetTempFileName throws IndexOutOfRangeException.  260 is the most you'd want.
            StringBuilder sb = new StringBuilder(260);

            Directory.CreateDirectory(basePath);
            uint result = GetTempFileName(basePath, prefix, unique, sb);
            if (result == 0)
            {
                var lastWin32Error = Marshal.GetLastWin32Error();
                if (lastWin32Error == 5)
                {
                    throw new IOException(string.Format(Resources.FileSaver_GetTempFileName_Access_Denied__unable_to_create_a_file_in_the_folder___0____Adjust_the_folder_write_permissions_or_retry_the_operation_after_moving_or_copying_files_to_a_different_folder_, basePath));
                }
                throw new IOException(TextUtil.LineSeparate(string.Format(Resources.FileSaver_GetTempFileName_Failed_attempting_to_create_a_temporary_file_in_the_folder__0__with_the_following_error_, basePath),
                        string.Format(Resources.FileSaver_GetTempFileName_Win32_Error__0__, lastWin32Error)));
            }
            return sb.ToString();
        }
    }
}