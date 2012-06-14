/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    [XmlRoot("ToolDescription")]
    public class ToolDescription : IXmlSerializable
    {
        public ToolDescription(string title, string command, string arguments, string initialDirectory)
        {
            Title = title;
            Command = command;
            Arguments = arguments;
            InitialDirectory = initialDirectory;
        }
        public string Title { get; set; }
        public string Command { get; set; }
        public string Arguments { get; set; }
        public string InitialDirectory { get; set; }

        #region Implementation of IXmlSerializable
        private  ToolDescription()
        {
        }

        public static ToolDescription Deserializer(XmlReader reader)
        {
            return reader.Deserialize(new ToolDescription());
        }

        private enum ATTR
        {
            title,
            command,
            arguments,
            initial_directory,
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            Title = reader.GetAttribute(ATTR.title);
            Command = reader.GetAttribute(ATTR.command);
            Arguments = reader.GetAttribute(ATTR.arguments);
            InitialDirectory = reader.GetAttribute(ATTR.initial_directory);
            reader.Read();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.title, Title);
            writer.WriteAttribute(ATTR.command, Command);
            writer.WriteAttribute(ATTR.arguments, Arguments);
            writer.WriteAttribute(ATTR.initial_directory, InitialDirectory);
        }
        #endregion
        public bool Equals(ToolDescription tool)
        {
            return (Title == tool.Title &&
                    Command == tool.Command &&
                    Arguments == tool.Arguments &&
                    InitialDirectory == tool.InitialDirectory);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Title.GetHashCode();
                result = (result * 397) ^ Command.GetHashCode();
                result = (result * 397) ^ Arguments.GetHashCode();
                result = (result * 397) ^ InitialDirectory.GetHashCode();
                return result;
            }
        }
    }
}