using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using SharedBatch.Properties;

namespace SharedBatch
{
    public enum SkylineType
    {
        Skyline,
        SkylineDaily,
        Local,
        Custom
    }
    
    public class SkylineSettings
    {
        // The skyline installation to use when a configuration is run
        public const string XML_EL = "config_skyline_settings";

        private int[] _version;

        // TODO(Ali): implement this later
        //private int[] _savedVersion;

        private List<string> _versionOutput;

        public SkylineSettings(SkylineType type, int[] savedVersion, string folderPath = "")
        {
            Type = type;
            _versionOutput = new List<string>();

            bool skylineAdminInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineAdminCmdPath);
            bool skylineWebInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineRunnerPath);
            bool skylineDailyAdminInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineDailyAdminCmdPath);
            bool skylineDailyWebInstallation = !string.IsNullOrEmpty(Settings.Default.SkylineDailyRunnerPath);

            //_savedVersion = savedVersion;

            switch (type)
            {
                case SkylineType.Skyline:
                    if (skylineWebInstallation)
                        CmdPath = Settings.Default.SkylineRunnerPath;
                    else if (skylineAdminInstallation)
                        CmdPath = Settings.Default.SkylineAdminCmdPath;
                    break;
                case SkylineType.SkylineDaily:
                    if (skylineDailyWebInstallation)
                        CmdPath = Settings.Default.SkylineDailyRunnerPath;
                    else if (skylineDailyAdminInstallation)
                        CmdPath = Settings.Default.SkylineDailyAdminCmdPath;
                    break;
                case SkylineType.Local:
                    CmdPath = Settings.Default.SkylineLocalCommandPath;
                    break;
                case SkylineType.Custom:
                    CmdPath = Path.Combine(folderPath, SkylineInstallations.SkylineCmdExe);
                    break;
            }
        }

        public readonly SkylineType Type; // The type of skyline installation
        public readonly string CmdPath; // the path to a SkylineCmd or SkylineRunner

        public void Validate()
        {
            if (!File.Exists(CmdPath))
            {
                switch (Type)
                {
                    case SkylineType.Skyline:
                        throw new ArgumentException(Resources.SkylineSettings_Validate_Could_not_find_a_Skyline_installation_on_this_computer_ + Environment.NewLine +
                                                    Resources.SkylineSettings_Validate_Please_try_a_different_Skyline_option_);
                    case SkylineType.SkylineDaily:
                        throw new ArgumentException(Resources.SkylineSettings_Validate_Could_not_find_a_Skyline_daily_installation_on_this_computer_ + Environment.NewLine +
                              Resources.SkylineSettings_Validate_Please_try_a_different_Skyline_option_); 
                    case SkylineType.Local:
                        throw new ArgumentException(string.Format(Resources.SkylineSettings_Validate_Could_not_find__0__at_this_location___1_, SkylineInstallations.SkylineCmdExe, CmdPath));
                    case SkylineType.Custom:
                        throw new ArgumentException(string.Format(Resources.SkylineSettings_Validate_Could_not_find_a_Skyline_installation_at_this_location___0_, Path.GetDirectoryName(CmdPath)) + Environment.NewLine +
                                                    string.Format(Resources.SkylineSettings_Validate_Please_select_a_folder_containing__0__, SkylineInstallations.SkylineCmdExe));
                }
            }
        }

        private enum Attr
        {
            type,
            version,
            path,

            // old xml tags
            Type,
            CmdPath,
        }

        public static SkylineSettings ReadXml(XmlReader reader)
        {
            var versionString = reader.GetAttribute(Attr.version);
            int[] savedVersion = null;
            if (!string.IsNullOrEmpty(versionString) &&
                !Equals(versionString, Resources.SkylineSettings_WriteXml_latest))
                savedVersion = ParseVersionFromString(versionString);
            // always use local Skyline if it exists
            if (SkylineInstallations.HasLocalSkylineCmd)
                return new SkylineSettings(SkylineType.Local, savedVersion);
            var type = Enum.Parse(typeof(SkylineType), reader.GetAttribute(Attr.type), false);
            var cmdPath = Path.GetDirectoryName(reader.GetAttribute(Attr.path));
            return new SkylineSettings((SkylineType)type, savedVersion, cmdPath);
        }

        public static SkylineSettings ReadXmlVersion_20_2(XmlReader reader)
        {
            var type = (SkylineType)Enum.Parse(typeof(SkylineType), reader.GetAttribute(Attr.Type), false);
            var cmdPath = reader.GetAttribute(Attr.CmdPath);
            if (type == SkylineType.Custom)
                return new SkylineSettings(type, null, cmdPath);
            return new SkylineSettings(type, null);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteStartElement(XML_EL);
            writer.WriteAttributeIfString(Attr.type, Type.ToString());
            writer.WriteAttributeIfString(Attr.version, _version != null ? string.Join(".", _version) : Resources.SkylineSettings_WriteXml_latest);
            if (Type == SkylineType.Custom)
                writer.WriteAttributeIfString(Attr.path, CmdPath);
            writer.WriteEndElement();
        }

        public async Task<int[]> GetVersion(ProcessRunner baseProcessRunner)
        {
            var output = "";
            var error = false;
            var versionCommand = "--version";
            //var processRunner = baseProcessRunner.Copy();
            var processRunner = new ProcessRunner()
            {
                OnDataReceived = (data) =>
                {
                    if (baseProcessRunner.OnDataReceived != null && data != null)
                    {
                        baseProcessRunner.OnDataReceived(data);
                        _versionOutput.Add(data);
                    }
                    if (data != null && !data.Contains(versionCommand) && string.IsNullOrEmpty(output))
                        output += data;
                },
                OnError = () =>
                {
                    if (baseProcessRunner.OnError != null) baseProcessRunner.OnError();
                    _versionOutput.Clear();
                    error = true;
                },
                OnException = baseProcessRunner.OnException
            };
            
            await processRunner.Run(CmdPath, versionCommand);
            var processEndTime = DateTime.Now;
            while (string.IsNullOrEmpty(output) && DateTime.Now - processEndTime < new TimeSpan(0, 0, 10))
                await Task.Delay(200);
            if (error || string.IsNullOrEmpty(output)) return null;

            var versionString = output.Split(' ');
            int i = 0;
            while (i < versionString.Length && (versionString[i].Length > 0 && !Int32.TryParse(versionString[i].Substring(0, 1), out _))) i++;
            if (i == versionString.Length)
                throw new Exception(Resources.SkylineSettings_GetVersion_No_parsable_Skyline_version_found_);
            return ParseVersionFromString(versionString[i]);
        }

        private static int[] ParseVersionFromString(string stringVersion)
        {
            var versionArray = stringVersion.Split('.');
            if (versionArray.Length != 4) throw new Exception(Resources.SkylineSettings_ParseVersionFromString_Error_parsing_Skyline_version_);
            var versionNumbers = new int[versionArray.Length];
            for (int i = 0; i < versionArray.Length; i++)
                versionNumbers[i] = Int32.Parse(versionArray[i]);
            return versionNumbers;
        }

        public async Task <bool> HigherVersion(string versionCutoff, ProcessRunner baseProcessRunner = null)
        {
            baseProcessRunner = baseProcessRunner ?? new ProcessRunner();
            var cutoff = ParseVersionFromString(versionCutoff);
            if (_version == null)
                _version = await GetVersion(baseProcessRunner);
            // log version output if it's already loaded
            else if (baseProcessRunner.OnDataReceived != null)
                foreach (var data in _versionOutput) baseProcessRunner.OnDataReceived(data);
            if (_version == null)
            {
                baseProcessRunner.OnDataReceived(Resources.SkylineSettings_HigherVersion_WARNING__Could_not_parse_Skyline_version__Running_earliest_supported_Skyline_commands_);
                return false; // could not parse version
            }
            for (int i = 0; i < cutoff.Length; i++)
            {
                if (_version[i] != cutoff[i]) return _version[i] > cutoff[i];
            }
            return true; // version is equal to cutoff
        }

        protected bool Equals(SkylineSettings other)
        {
            if (Type == SkylineType.Custom && other.Type == SkylineType.Custom)
                return CmdPath.Equals(other.CmdPath);
            return Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SkylineSettings)obj);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode();
        }
    }
}
