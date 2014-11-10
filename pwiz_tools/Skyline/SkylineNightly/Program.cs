using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Ionic.Zip;
using SkylineNightly.Properties;

namespace SkylineNightly
{
// ReSharper disable NonLocalizedString
    static class Program
    {
        public const string NIGHTLY_TASK_NAME = "Skyline nightly build";
        public const string SCHEDULED_ARG = "scheduled";

        private const string TEAM_CITY_BUILD_URL = "https://teamcity.labkey.org/viewType.html?buildTypeId=bt{0}";
        private const string TEAM_CITY_ZIP_URL = "https://teamcity.labkey.org/repository/download/bt{0}/{1}:id/SkylineTester.zip";
        private const int TEAM_CITY_BUILD_TYPE_64 = 209;
        private const int TEAM_CITY_BUILD_TYPE_32 = 19;
        private const int TEAM_CITY_BUILD_TYPE = TEAM_CITY_BUILD_TYPE_64;
        private const string TEAM_CITY_USER_NAME = "guest";
        private const string TEAM_CITY_USER_PASSWORD = "guest";

        private static string _logFile; 

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && string.Compare(args[0], SCHEDULED_ARG, StringComparison.OrdinalIgnoreCase) == 0)
            {
                StartNightly();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SkylineNightly());
        }

        private static void StartNightly()
        {
            var nightlyDir = Settings.Default.NightlyFolder;
            if (!Path.IsPathRooted(nightlyDir))
                nightlyDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), nightlyDir);
            var logDir = Path.Combine(nightlyDir, "Logs");
            var pwizDir = Path.Combine(nightlyDir, "pwiz");
            var skylineTesterDir = Path.Combine(nightlyDir, "SkylineTester");
            var skylineNightlySkytr = Path.Combine(nightlyDir, "SkylineNightly.skytr");

            // Create log file.
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
            _logFile = Path.Combine(nightlyDir, "SkylineNightly.log");
            Delete(_logFile);
            Log(DateTime.Now.ToShortDateString());

            Delete(skylineNightlySkytr);
            Log("Delete pwiz folder");
            Delete(pwizDir);
            Log("Delete SkylineTester");
            Delete(skylineTesterDir);

            // Download most recent build of SkylineTester.
            var skylineTesterZip = skylineTesterDir + ".zip";
            using (var client = new WebClient())
            {
                client.Credentials = new NetworkCredential(TEAM_CITY_USER_NAME, TEAM_CITY_USER_PASSWORD);

                var buildPageUrl = string.Format(TEAM_CITY_BUILD_URL, TEAM_CITY_BUILD_TYPE);
                Log("Download Team City build page");
                var buildStatusPage = client.DownloadString(buildPageUrl);

                var match = Regex.Match(buildStatusPage, @"<span\sid=""build:([^""]*):text"">Tests\spassed:");
                if (match.Success)
                {
                    string id = match.Groups[1].Value;
                    string zipFileLink = string.Format(TEAM_CITY_ZIP_URL, TEAM_CITY_BUILD_TYPE, id);
                    Log("Download SkylineTester zip file");
                    client.DownloadFile(zipFileLink, skylineTesterZip);
                }
            }

            // Install SkylineTester.
            using (var zipFile = new ZipFile(skylineTesterZip))
            {
                try
                {
                    Log("Unzip SkylineTester");
                    zipFile.ExtractAll(skylineTesterDir, ExtractExistingFileAction.OverwriteSilently);
                }
                catch (Exception)
                {
                    Log("Error attempting to unzip SkylineTester");
                    return;
                }
            }

            // Delete zip file.
            Log("Delete zip file");
            File.Delete(skylineTesterZip);

            // Create ".skytr" file to execute nightly build in SkylineTester.
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "SkylineNightly.SkylineNightly.skytr";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Log("Embedded resource is broken");
                    return;
                }
                using (StreamReader reader = new StreamReader(stream))
                {
                    var xml = new Xml(reader.ReadToEnd(), "SkylineTester");
                    xml["nightlyStartTime"] = DateTime.Now.ToShortTimeString();
                    xml["nightlyRoot"] = pwizDir;
                    xml["nightlyLogs"] = logDir;
                    xml.Save(skylineNightlySkytr);
                }
            }

            // Start SkylineTester to do the build.
            var skylineTesterExe = Path.Combine(skylineTesterDir, "SkylineTester Files", "SkylineTester.exe");

            var processInfo = new ProcessStartInfo(skylineTesterExe, skylineNightlySkytr)
            {
                WorkingDirectory = Path.GetDirectoryName(skylineTesterExe) ?? ""
            };

            var skylineTesterProcess = Process.Start(processInfo);
            if (skylineTesterProcess == null)
                Log("SkylineTester did not start");
            else
            {
                Log("SkylineTester started");
                skylineTesterProcess.WaitForExit();
                Log("SkylineTester finished");
            }
        }

        private static void Delete(string fileOrDir)
        {
            try
            {
                if (File.Exists(fileOrDir))
                    File.Delete(fileOrDir);
                else if (Directory.Exists(fileOrDir))
                    Directory.Delete(fileOrDir, true);
            }
            catch (Exception)
            {
                Log("Problem deleting " + fileOrDir);
                throw;
            }
        }

        private static void Log(string message)
        {
            var time = DateTime.Now;
            File.AppendAllText(_logFile, string.Format(
                "[{0}:{1}:{2}] {3}",
                time.Hour.ToString("D2"),
                time.Minute.ToString("D2"),
                time.Second.ToString("D2"),
                message)
                + Environment.NewLine);
        }

        private class Xml
        {
            private readonly XmlDocument _doc;
            private readonly string _rootElement;

            public Xml(string xml, string rootElement)
            {
                _doc = new XmlDocument();
                _doc.LoadXml(xml);
                _rootElement = rootElement;
            }

            public string this[string key]
            {
                set
                {
                    var node = _doc.SelectSingleNode("/" + _rootElement + "/" + key);
                    if (node != null)
                        node.InnerText = value;
                }
            }

            public void Save(string file)
            {
                _doc.Save(file);
            }
        }
    }
    // ReSharper restore NonLocalizedString
}
