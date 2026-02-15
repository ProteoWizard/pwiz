/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public enum SearchToolType
    {
        CruxComet,
        CruxPercolator,
        CruxTide,
        DIANN,
        EncyclopeDIA,
        Java,
        Java8,
        MSFragger,
        MSGFPlus
    }
    
    [XmlRoot("search_tool")]
    public sealed class SearchTool : Immutable, IKeyContainer<string>, IXmlSerializable
    {
        /// <summary>
        /// The enum-based value for this tool in the SearchTool system (that allows users to provide their own local versions of tools).
        /// </summary>
        public SearchToolType Name { get; set; }
        
        /// <summary>
        /// The final path to the executable tool (e.g. an exe or script file).
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// Extra arguments that are always passed to the tool (e.g. Java heap parameters).
        /// </summary>
        public string ExtraCommandlineArgs { get; set; }
        
        /// <summary>
        /// The path the tool is installed to.
        /// </summary>
        public string InstallPath { get; set; }
        
        /// <summary>
        /// True if Skyline downloaded and installed the tool.
        /// </summary>
        public bool AutoInstalled { get; set; }

        public SearchTool(SearchToolType name, string path, string extraCommandlineArgs, string installPath, bool autoInstalled)
        {
            Name = name;
            Path = path;
            ExtraCommandlineArgs = extraCommandlineArgs;
            InstallPath = installPath;
            AutoInstalled = autoInstalled;
        }

        public string GetKey()
        {
            return Name.ToString();
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private SearchTool()
        {
        }

        private enum ATTR
        {
            name,
            path,
            args,
            install_path,
            auto_installed
        }

        public static SearchTool Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new SearchTool());
        }

        private void Validate()
        {
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Name = reader.GetRequiredEnumAttribute<SearchToolType>(ATTR.name, XmlUtil.EnumCase.unkown);
            Path = reader.GetAttribute(ATTR.path);
            ExtraCommandlineArgs = reader.GetAttribute(ATTR.args);
            InstallPath = reader.GetAttribute(ATTR.install_path);
            AutoInstalled = reader.GetBoolAttribute(ATTR.auto_installed);
            // Consume tag
            reader.Read();

            Validate();
        }

        public void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttribute(ATTR.name, Name);
            writer.WriteAttribute(ATTR.path, Path);
            writer.WriteAttribute(ATTR.args, ExtraCommandlineArgs);
            writer.WriteAttribute(ATTR.install_path, InstallPath);
            writer.WriteAttribute(ATTR.auto_installed, AutoInstalled);
        }
        #endregion

        #region object overrides

        private bool Equals(SearchTool other)
        {
            return Equals(Name, other.Name) &&
                   Equals(Path, other.Path) &&
                   Equals(ExtraCommandlineArgs, other.ExtraCommandlineArgs) &&
                   Equals(InstallPath, other.InstallPath) &&
                   Equals(AutoInstalled, other.AutoInstalled);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is SearchTool && Equals((SearchTool)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Name.GetHashCode();
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ExtraCommandlineArgs != null ? ExtraCommandlineArgs.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InstallPath != null ? InstallPath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ AutoInstalled.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}