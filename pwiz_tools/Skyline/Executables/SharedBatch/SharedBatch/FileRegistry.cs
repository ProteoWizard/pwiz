using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace SharedBatch
{
    public class FileRegistry
    {

        public static void AddFileType(string extension, string id, string description, string exePath, string iconPath)
        {
            // Register file/exe/icon associations.
            var checkRegistry = Registry.GetValue(
                $@"HKEY_CURRENT_USER\Software\Classes\{id}\shell\open\command", null, null);

            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}", null, description);
            
            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{id}\DefaultIcon", null,
                $"\"{iconPath}\"");

            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\SkylineBatch.Configuration.0\shell\open\command", null,
                $"\"{exePath}\" \"%1\"");
            Registry.SetValue($@"HKEY_CURRENT_USER\Software\Classes\{extension}", null, id);


            //if (checkRegistry == null)
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    }
}
