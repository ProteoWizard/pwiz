using System;
using System.IO;

namespace InstallVSPlugin
{
    class Program
    {
        static void Main()
        {
            var destinationFilePath = string.Format(@"{0}\JetBrains\ReSharper\v8.2\vs10.0\plugins\LocalizationHelper.dll", // Not L10N
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                File.Copy(@"plugins\LocalizationHelper.dll", destinationFilePath, true); // Not L10N
        }
    }
}
