using System;
using System.IO;

namespace InstallVSPlugin
{
    class Program
    {
        static void Main()
        {
            // ReSharper disable once LocalizableElement
            var destinationFilePath = string.Format(@"{0}\JetBrains\ReSharper\v8.2\vs10.0\plugins\LocalizationHelper.dll",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                // ReSharper disable once LocalizableElement
                File.Copy(@"plugins\LocalizationHelper.dll", destinationFilePath, true);
        }
    }
}
