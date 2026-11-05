/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// A model in Skyline's list of Carafe models: a fine-tuned model file CarafeSharp saved
    /// (<c>.model.zip</c>), under the name the user gave it, or the default, the pretrained
    /// AlphaPeptDeep models, which has no file. Only the name and the path are kept; what the
    /// model was trained on is read from the file when it is shown, so a moved or deleted file
    /// shows as missing rather than as what it once held.
    /// </summary>
    [XmlRoot("carafe_model")]
    public class CarafeModelSpec : XmlNamedElement
    {
        /// <summary>The pretrained AlphaPeptDeep models, first in every list and not editable.</summary>
        public static readonly CarafeModelSpec DEFAULT = new CarafeModelSpec(@"AlphaPeptDeep", null);

        public CarafeModelSpec(string name, string filePath)
            : base(name)
        {
            FilePath = filePath;
        }

        private CarafeModelSpec()
        {
        }

        private enum Attr
        {
            file_path
        }

        /// <summary>The fine-tuned model file, or null for the pretrained models.</summary>
        public string FilePath { get; private set; }

        public bool IsDefault
        {
            get { return FilePath == null; }
        }

        public CarafeModelSpec ChangeFilePath(string filePath)
        {
            return ChangeProp(ImClone(this), im => im.FilePath = filePath);
        }

        public static CarafeModelSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new CarafeModelSpec());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            FilePath = reader.GetAttribute(Attr.file_path);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeIfString(Attr.file_path, FilePath);
        }

        protected bool Equals(CarafeModelSpec other)
        {
            return base.Equals(other) && Equals(FilePath, other.FilePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is CarafeModelSpec other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
            }
        }
    }
}
