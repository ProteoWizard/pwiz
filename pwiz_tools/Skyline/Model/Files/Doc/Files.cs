/*
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

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Files.Doc
{
    // TODO: should SrmSettings.Files be marked with [TrackChildren]? Ex: in SrmSettings, PeptideSettings is, MeasuredResults is not
    // CONSIDER: should <file> have an optional "name=" attribute to support setting a human-readable file name?
    // CONSIDER: <files> is generic - so maybe this should use a more specific root element name? Examples: <other-files/> or <user-file/>
    [XmlRoot("files")]
    public class Files : Immutable, IXmlSerializable
    {
        public static Files DEFAULT = new Files();

        public Files(IList<File> fileList)
        {
            FileList = fileList;
        }

        public IList<File> FileList { get; private set; }

        public bool IsEmpty => FileList.Count == 0;

        public int Count => FileList.Count;

        public File FindById(Identity id)
        {    
            return FileList.Where(item => ReferenceEquals(item.Id, id)).ToList().First();
        }

        public Files ChangeFileList(IList<File> fileList)
        {
            return ChangeProp(ImClone(this), im => im.FileList = fileList);
        }

        #region Implementation of IXmlSerializable

        private static readonly IXmlElementHelper<File>[] HELPERS = { new XmlElementHelper<File>() };

        private Files()
        {
            FileList = ImmutableList.Empty<File>();
        }

        public static Files Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new Files());
        }

        public void ReadXml(XmlReader reader)
        {
            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();

                var list = new List<File>();

                while (reader.FindHelper(HELPERS) is { } helperFile)
                {
                    list.Add(helperFile.Deserialize(reader));
                }

                reader.ReadEndElement();

                FileList = ImmutableList<File>.ValueOf(list);
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var file in FileList)
            {
                writer.WriteElement(file);
            }
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        #endregion

        #region object overrides

        public override int GetHashCode()
        {
            return FileList.GetHashCodeDeep();
        }

        public bool Equals(Files obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            return ArrayUtil.EqualsDeep(obj.FileList, FileList);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(Files)) return false;
            return Equals((Files)obj);
        }

        #endregion
    }

    [XmlRoot("file")]
    public class File : Immutable, IXmlSerializable
    {
        private sealed class FileId : Identity { }

        public File(string name) : this()
        {
            Name = name;
        }

        public Identity Id { get; private set; }
        public string Name { get; private set; }

        #region Implementation of IXmlSerializable

        private File()
        {
            Id = new FileId();
        }

        private enum ATTR { name }

        public static File Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new File());
        }

        public void ReadXml(XmlReader reader)
        {
            Name = reader.GetAttribute(ATTR.name.ToString());
            reader.Read(); // consume tag
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttributeString(ATTR.name.ToString(), Name);
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        #endregion

        #region object overrides

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public bool Equals(File obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(File)) return false;
            return Equals((File)obj);
        }

        #endregion
    }
}