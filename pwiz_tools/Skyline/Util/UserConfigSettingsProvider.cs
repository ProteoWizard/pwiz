/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Xml.Linq;
using pwiz.Skyline.Util;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Keeps user scoped settings in a "user.config" file whose folder this class chooses,
    /// instead of the per user, per version folder under %LOCALAPPDATA% that
    /// <see cref="LocalFileSettingsProvider"/> generates a name for.
    ///
    /// The file format is the same one <see cref="LocalFileSettingsProvider"/> writes, so an
    /// existing user.config can be copied into place and read by this provider. Each settings
    /// class gets its own section, named for the class, which lets several settings classes
    /// share one file.
    ///
    /// Application scoped settings are not affected: they are still read from the .config file
    /// beside the executable, by handing those properties to a <see cref="LocalFileSettingsProvider"/>.
    ///
    /// Attach this to a settings class with
    /// <code>[SettingsProvider(typeof(UserConfigSettingsProvider))]</code>. On a class generated
    /// by the settings designer, put the attribute on the hand written half of the partial class
    /// so that regenerating the designer file does not discard it.
    ///
    /// Settings declared with SettingsSerializeAs.Binary are not handled, because on net10 the
    /// framework throws out of SettingsPropertyValue.SerializedValue for those before this class
    /// ever sees them: BinaryFormatter no longer has an implementation to call. String and Xml,
    /// which is everything Skyline uses, are what this reads and writes.
    /// </summary>
    public class UserConfigSettingsProvider : SettingsProvider, IApplicationSettingsProvider
    {
        public const string CONFIG_FILE_NAME = @"user.config";

        private const string EL_CONFIGURATION = @"configuration";
        private const string EL_USER_SETTINGS = @"userSettings";
        private const string EL_SETTING = @"setting";
        private const string EL_VALUE = @"value";
        private const string ATTR_NAME = @"name";
        private const string ATTR_SERIALIZE_AS = @"serializeAs";
        private const string KEY_GROUP_NAME = @"GroupName";

        /// <summary>
        /// File beside the executable listing the users who may use the user.config there,
        /// instead of the folder's owner. See <see cref="IsFolderOwner"/>.
        /// </summary>
        public const string FOLDER_OWNERS_FILE_NAME = @"folderowners.txt";

        private static string _defaultConfigFolder;

        /// <summary>
        /// The folder holding the assembly this code lives in. Skyline.exe, Skyline-daily.exe and
        /// SkylineCmd.exe are all installed beside that assembly, so they all resolve to one
        /// user.config and share their settings.
        ///
        /// When the process is something else entirely, such as a test host, this is that host's
        /// folder.
        ///
        /// Note that this deliberately asks where the assembly is rather than where the entry
        /// executable is. Use <see cref="AppContext.BaseDirectory"/> instead if this assembly ever
        /// stops being deployed next to the executable.
        /// </summary>
        public static string GetInstallationFolder()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// Where the running installation keeps the current user's settings, by the rule in
        /// <see cref="GetConfigFolder"/>.
        /// </summary>
        public static string GetDefaultConfigFolder()
        {
            return _defaultConfigFolder ?? (_defaultConfigFolder = GetConfigFolder(GetInstallationFolder()));
        }

        /// <summary>
        /// Where an installation in the folder keeps the current user's settings. A user who owns
        /// the folder, as with a per user install or a build, keeps them beside the executable.
        /// Anyone else, as with a per machine install under Program Files, keeps them under
        /// %LOCALAPPDATA%, whether or not the folder happens to be writable to them; the
        /// user.config beside the executable is then the administrator's, shared by every user
        /// of the installation. An elevated administrator owns a folder the installer made, so
        /// that is how the shared file gets written. See <see cref="IsFolderOwner"/> for who
        /// counts as an owner.
        /// </summary>
        public static string GetConfigFolder(string installationFolder)
        {
            return IsFolderOwner(installationFolder)
                ? installationFolder
                : GetPersonalConfigFolder(installationFolder);
        }

        /// <summary>
        /// The administrator's user.config beside the running executable, for a user whose own
        /// settings are elsewhere and start from it, or null when the user's settings are that
        /// file or there is none.
        /// </summary>
        public static string GetSharedConfigFile()
        {
            var installationFolder = GetInstallationFolder();
            if (string.Equals(GetDefaultConfigFolder(), installationFolder, StringComparison.OrdinalIgnoreCase))
                return null;
            var sharedConfigFile = Path.Combine(installationFolder, CONFIG_FILE_NAME);
            return File.Exists(sharedConfigFile) ? sharedConfigFile : null;
        }

        /// <summary>
        /// The folder under %LOCALAPPDATA%\Skyline holding the settings and tools of an
        /// installation the user does not own. A per machine install sits directly under
        /// Program Files, where its folder name is unique, so the folder name is used alone.
        /// Anywhere else, two installations can share a folder name,
        /// such as the Debug and Release builds' net10.0-windows, so a checksum of the whole
        /// path follows it. The path is upper cased first because Windows paths ignore case,
        /// and one installation must not get two folders by being launched as I:\Git and I:\git.
        /// </summary>
        public static string GetPersonalConfigFolder(string installationFolder)
        {
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationFolder));
            var folderName = Path.GetFileName(fullPath);
            if (!string.Equals(Path.GetDirectoryName(fullPath),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase))
            {
                folderName += @"_" + AdlerChecksum.MakeForString(fullPath.ToUpperInvariant());
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Skyline", folderName);
        }

        /// <summary>
        /// Whether the current user may use the user.config in the folder. A
        /// <see cref="FOLDER_OWNERS_FILE_NAME"/> in the folder decides this when there is one:
        /// it lists one user name per line, either the bare name or DOMAIN\name (DOMAIN/name is
        /// accepted too), and anyone not listed gets settings of their own, so an empty file
        /// gives everyone their own, as does a file that cannot be read. Without that file, the
        /// folder's owner decides, by <see cref="IsOwnedByCurrentUser"/>. The file makes it easy
        /// to try the shared settings arrangement without changing a folder's owner, which takes
        /// elevation.
        /// </summary>
        public static bool IsFolderOwner(string folder)
        {
            var ownersFile = Path.Combine(folder, FOLDER_OWNERS_FILE_NAME);
            if (!File.Exists(ownersFile))
                return IsOwnedByCurrentUser(folder);
            string[] ownerNames;
            try
            {
                ownerNames = File.ReadAllLines(ownersFile);
            }
            catch (Exception)
            {
                return false;
            }
            if (ownerNames.Length == 0)
                return false;
            using (var identity = WindowsIdentity.GetCurrent())
            {
                return ownerNames.Any(ownerName => IsUserName(ownerName, identity.Name));
            }
        }

        /// <summary>
        /// Whether the folder's owner is the current user, or a group the current user is an
        /// enabled member of. The latter is how an elevated administrator owns a folder owned by
        /// Administrators; the same administrator without elevation does not. A folder whose
        /// owner cannot be read, such as one on a file system without security, counts as owned,
        /// which keeps the settings beside the executable.
        /// </summary>
        public static bool IsOwnedByCurrentUser(string folder)
        {
            try
            {
                var owner = new DirectoryInfo(folder).GetAccessControl().GetOwner(typeof(SecurityIdentifier))
                    as SecurityIdentifier;
                if (owner == null)
                    return true;
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    return owner.Equals(identity.User) || new WindowsPrincipal(identity).IsInRole(owner);
                }
            }
            catch (Exception)
            {
                return true;
            }
        }

        private readonly LocalFileSettingsProvider _applicationScopedProvider = new LocalFileSettingsProvider();
        private string _configFolder;

        /// <summary>
        /// Folder that holds user.config. Defaults to <see cref="GetDefaultConfigFolder"/>.
        /// Set this to store settings somewhere else, which is what a test wanting its own
        /// settings file should do.
        /// </summary>
        public string ConfigFolder
        {
            get { return _configFolder ?? (_configFolder = GetDefaultConfigFolder()); }
            set { _configFolder = value; }
        }

        /// <summary>
        /// Name of the settings file in <see cref="ConfigFolder"/>. Something other than
        /// user.config only for reading a copy of one, as the settings merge does.
        /// </summary>
        public string ConfigFileName { get; set; } = CONFIG_FILE_NAME;

        public string ConfigFilePath
        {
            get { return Path.Combine(ConfigFolder, ConfigFileName); }
        }

        public override string ApplicationName { get; set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name ?? GetType().Name, config);
            _applicationScopedProvider.Initialize(name, config);
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context,
            SettingsPropertyCollection properties)
        {
            var applicationScoped = new SettingsPropertyCollection();
            var userScoped = new List<SettingsProperty>();
            foreach (SettingsProperty property in properties)
            {
                if (IsUserScoped(property))
                    userScoped.Add(property);
                else
                    applicationScoped.Add(property);
            }

            var values = new SettingsPropertyValueCollection();
            if (applicationScoped.Count > 0)
            {
                foreach (SettingsPropertyValue value in
                         _applicationScopedProvider.GetPropertyValues(context, applicationScoped))
                {
                    values.Add(value);
                }
            }
            if (userScoped.Count > 0)
            {
                var storedValues = ReadStoredValues(GetSectionName(context));
                foreach (var property in userScoped)
                    values.Add(CreateUserScopedValue(property, storedValues));
            }
            return values;
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection values)
        {
            var document = LoadDocument() ?? new XDocument(new XElement(EL_CONFIGURATION));
            var section = GetOrCreateSection(document, GetSectionName(context));
            // A setting earns a place in the file the first time it is changed, and keeps it
            // afterwards. Skyline has several hundred settings and a user changes a handful, so
            // writing them all would bury the interesting ones, and writing only the dirty ones
            // would drop everything saved by an earlier run.
            var alreadyStored = new HashSet<string>(section.Elements(EL_SETTING)
                .Select(settingElement => (string) settingElement.Attribute(ATTR_NAME))
                .Where(name => !string.IsNullOrEmpty(name)));
            section.RemoveNodes();
            foreach (SettingsPropertyValue value in values)
            {
                if (IsUserScoped(value.Property) && (value.IsDirty || alreadyStored.Contains(value.Name)))
                    section.Add(CreateSettingElement(value));
            }
            SaveDocument(document);
        }

        /// <summary>
        /// There is no previous version to read from: the file path does not include a version
        /// number, so every version of the program reads the same file.
        /// </summary>
        public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            return new SettingsPropertyValue(property) { PropertyValue = null };
        }

        /// <summary>
        /// Nothing to upgrade, for the reason given on <see cref="GetPreviousVersion"/>. Callers
        /// still invoke this by way of ApplicationSettingsBase.Upgrade.
        /// </summary>
        public void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        {
        }

        public void Reset(SettingsContext context)
        {
            var document = LoadDocument();
            if (document == null)
                return;
            var section = FindSection(document, GetSectionName(context));
            if (section == null)
                return;
            section.Remove();
            SaveDocument(document);
        }

        private static bool IsUserScoped(SettingsProperty property)
        {
            return property.Attributes[typeof(UserScopedSettingAttribute)] != null;
        }

        private static string GetSectionName(SettingsContext context)
        {
            var groupName = context?[KEY_GROUP_NAME] as string;
            if (string.IsNullOrEmpty(groupName))
            {
                // ApplicationSettingsBase always supplies this. Reaching here means the provider
                // was attached to something that does not, and there is no name to file the
                // settings under.
                throw new InvalidOperationException(
                    $@"No {KEY_GROUP_NAME} in the settings context, so the section of {CONFIG_FILE_NAME} to use is unknown.");
            }
            return groupName;
        }

        private static SettingsPropertyValue CreateUserScopedValue(SettingsProperty property,
            IDictionary<string, string> storedValues)
        {
            var value = new SettingsPropertyValue(property);
            if (storedValues.TryGetValue(property.Name, out var storedValue))
            {
                value.SerializedValue = storedValue;
                value.IsDirty = false;
            }
            return value;
        }

        private static XElement CreateSettingElement(SettingsPropertyValue value)
        {
            var valueElement = new XElement(EL_VALUE);
            var serializedValue = value.SerializedValue;
            if (value.Property.SerializeAs == SettingsSerializeAs.Xml)
            {
                // The serialized form is a document, whose root element gets stored on its own so
                // that the XML declaration does not end up inside user.config.
                var text = serializedValue as string;
                if (!string.IsNullOrEmpty(text))
                    valueElement.Add(XDocument.Parse(text).Root);
            }
            else
            {
                valueElement.Add(serializedValue as string ?? string.Empty);
            }
            return new XElement(EL_SETTING,
                new XAttribute(ATTR_NAME, value.Name),
                new XAttribute(ATTR_SERIALIZE_AS, value.Property.SerializeAs.ToString()),
                valueElement);
        }

        private IDictionary<string, string> ReadStoredValues(string sectionName)
        {
            var storedValues = new Dictionary<string, string>();
            var document = LoadDocument();
            var section = document == null ? null : FindSection(document, sectionName);
            if (section == null)
                return storedValues;
            foreach (var settingElement in section.Elements(EL_SETTING))
            {
                var name = (string) settingElement.Attribute(ATTR_NAME);
                var valueElement = settingElement.Element(EL_VALUE);
                if (string.IsNullOrEmpty(name) || valueElement == null)
                    continue;
                // An Xml serialized setting holds an element; every other kind holds text.
                var childElement = valueElement.Elements().FirstOrDefault();
                storedValues[name] = childElement?.ToString() ?? valueElement.Value;
            }
            return storedValues;
        }

        private static XElement FindSection(XDocument document, string sectionName)
        {
            return document.Root?.Element(EL_USER_SETTINGS)?.Element(sectionName);
        }

        private static XElement GetOrCreateSection(XDocument document, string sectionName)
        {
            var root = document.Root;
            if (root == null)
            {
                root = new XElement(EL_CONFIGURATION);
                document.Add(root);
            }
            var userSettings = root.Element(EL_USER_SETTINGS);
            if (userSettings == null)
            {
                userSettings = new XElement(EL_USER_SETTINGS);
                root.Add(userSettings);
            }
            var section = userSettings.Element(sectionName);
            if (section == null)
            {
                section = new XElement(sectionName);
                userSettings.Add(section);
            }
            return section;
        }

        private XDocument LoadDocument()
        {
            var path = ConfigFilePath;
            if (!File.Exists(path))
                return null;
            try
            {
                return XDocument.Load(path);
            }
            catch (Exception)
            {
                // A corrupt or unreadable user.config must not stop the program from starting.
                // Settings fall back to their defaults and the file is rewritten on the next save.
                return null;
            }
        }

        /// <summary>
        /// Writes to a temporary file and moves it into place, so that a failure partway through
        /// leaves the last good user.config rather than a truncated one.
        /// </summary>
        private void SaveDocument(XDocument document)
        {
            var path = ConfigFilePath;
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            var tempPath = path + @".tmp";
            document.Save(tempPath);
            File.Move(tempPath, path, true);
        }

        /// <summary>
        /// Whether a line of <see cref="FOLDER_OWNERS_FILE_NAME"/> names the user whose
        /// DOMAIN\name is given. A bare name matches the user in any domain.
        /// </summary>
        private static bool IsUserName(string ownerName, string domainUserName)
        {
            ownerName = ownerName.Trim().Replace('/', '\\');
            if (ownerName.Length == 0)
                return false;
            if (!ownerName.Contains('\\'))
                domainUserName = domainUserName.Substring(domainUserName.LastIndexOf('\\') + 1);
            return string.Equals(ownerName, domainUserName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
