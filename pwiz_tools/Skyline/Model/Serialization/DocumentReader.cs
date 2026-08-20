/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Google.Protobuf;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    public class DocumentReader : DocumentSerializer
    {
        private readonly StringPool _stringPool = new StringPool();
        private AnnotationScrubber _annotationScrubber;
        public DocumentFormat FormatVersion
        {
            get { return DocumentFormat; }
            private set
            {
                DocumentFormat = value;
            }
        }
        public PeptideGroupDocNode[] Children { get; private set; }

        /// <summary>
        /// In older versions of Skyline we would handle ion notation by building it into the molecule, 
        /// so our current C12H5[M+2H] would have been C12H7 - this requires special handling on read
        /// </summary>
        public bool DocumentMayContainMoleculesWithEmbeddedIons { get { return FormatVersion <= DocumentFormat.VERSION_3_71; } }

        public bool RemoveCalculatedAnnotationValues { get; set; } = true;

        /// <summary>
        /// Avoids duplication of species strings
        /// </summary>
        public string GetUniqueSpecies(string species)
        {
            return _stringPool.GetString(species);
        }

        /// <summary>
        /// One string object for every occurrence of the same text, which is what keeps a document
        /// full of repeated names from holding a copy of each. Shared by the
        /// <see cref="MoleculeReader"/> instances, so that the pooling covers the whole document
        /// rather than one molecule.
        /// </summary>
        internal string GetUniqueString(string value)
        {
            return _stringPool.GetString(value);
        }

        /// <summary>
        /// What the annotations of everything below a molecule are scrubbed with. Made once the
        /// settings have been read - see <see cref="ReadXml"/>.
        /// </summary>
        internal AnnotationScrubber AnnotationScrubber
        {
            get { return _annotationScrubber; }
        }




        public Annotations ReadTargetAnnotations(XmlReader reader, AnnotationDef.AnnotationTarget target)
        {
            var annotations = ReadAnnotations(reader);
            return _annotationScrubber.ScrubAnnotations(annotations, target);
        }

        /// <summary>
        /// Reads annotations from XML. The annotations should later be passed through
        /// <see cref="AnnotationScrubber.ScrubAnnotations"/> to ensure that the keys use a single
        /// string object and also that calculated annotations are removed.
        /// </summary>
        public static Annotations ReadAnnotations(XmlReader reader)
        {
            string note = null;
            int color = Annotations.EMPTY.ColorIndex;
            var annotations = new Dictionary<string, string>();
            
            if (reader.IsStartElement(EL.note))
            {
                color = reader.GetIntAttribute(ATTR.category);
                note = reader.ReadElementString();
            }
            while (reader.IsStartElement(EL.annotation))
            {
                string name = reader.GetAttribute(ATTR.name);
                if (name == null)
                    throw new InvalidDataException(SerializationResources.SrmDocument_ReadAnnotations_Annotation_found_without_name);
                annotations[name] = reader.ReadElementString();
            }

            return note != null || annotations.Count > 0
                ? new Annotations(note, annotations, color)
                : Annotations.EMPTY;
        }



        /// <summary>
        /// Deserializes document from XML.
        /// </summary>
        /// <param name="reader">The reader positioned at the document start tag</param>
        public void ReadXml(XmlReader reader)
        {
            double formatVersionNumber = reader.GetDoubleAttribute(ATTR.format_version);
            if (formatVersionNumber == 0)
            {
                FormatVersion = DocumentFormat.VERSION_0_1;
            }
            else
            {
                FormatVersion = new DocumentFormat(formatVersionNumber);
                if (FormatVersion.CompareTo(DocumentFormat.CURRENT) > 0)
                {
// Resharper disable ImpureMethodCallOnReadonlyValueField
                    throw new VersionNewerException(
                        string.Format(SerializationResources.SrmDocument_ReadXml_The_document_format_version__0__is_newer_than_the_version__1__supported_by__2__,
                            formatVersionNumber, DocumentFormat.CURRENT.AsDouble(), Install.ProgramNameAndVersion));
// ReSharper restore ImpureMethodCallOnReadonlyValueField
                }
            }

            reader.ReadStartElement();  // Start document element
            var srmSettings = reader.DeserializeElement<SrmSettings>() ?? SrmSettingsList.GetDefault();
            _annotationScrubber = AnnotationScrubber.MakeAnnotationScrubber(_stringPool, srmSettings.DataSettings, RemoveCalculatedAnnotationValues);
            srmSettings = _annotationScrubber.ScrubSrmSettings(srmSettings);
            Settings = srmSettings;
            using var peptideProcessor = new PeptideProcessor(this);
            var peptideGroupDatas = new List<PeptideGroupData>();
            if (reader.IsStartElement())
            {
                // Support v0.1 naming
                if (!reader.IsStartElement(EL.selected_proteins))
                    peptideGroupDatas.AddRange(ReadPeptideGroupListXml(reader, peptideProcessor));
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement();
                    peptideGroupDatas.AddRange(ReadPeptideGroupListXml(reader, peptideProcessor));
                    reader.ReadEndElement();
                }

                if (reader.IsStartElement(AuditLogList.XML_ROOT))
                    reader.Skip();
            }

            reader.ReadEndElement();    // End document element
            peptideProcessor.WaitUntilFinished();
            Children = peptideGroupDatas.Select(p => p.MakePeptideGroupDocNode()).ToArray();
        }

        /// <summary>
        /// Deserializes an array of <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at the start of the list.
        /// </summary>
        private IEnumerable<PeptideGroupData> ReadPeptideGroupListXml(XmlReader reader, PeptideProcessor peptideProcessor)
        {
            while (reader.IsStartElement(EL.protein) || reader.IsStartElement(EL.peptide_list) || reader.IsStartElement(EL.protein_group))
            {
                if (reader.IsStartElement(EL.protein))
                    yield return ReadProteinXml(reader, peptideProcessor);
                else if (reader.IsStartElement(EL.protein_group))
                    yield return ReadProteinGroupXml(reader, peptideProcessor);
                else
                    yield return ReadPeptideGroupXml(reader, peptideProcessor);
            }
        }

        private ProteinMetadata ReadProteinMetadataXML(XmlReader reader, bool labelNameAndDescription)
        {
            var labelPrefix = labelNameAndDescription ? @"label_" : string.Empty;
            return new ProteinMetadata(
                reader.GetAttribute(labelPrefix + ATTR.name),
                reader.GetAttribute(labelPrefix + ATTR.description),
                reader.GetAttribute(ATTR.preferred_name),
                reader.GetAttribute(ATTR.accession),
                reader.GetAttribute(ATTR.gene),
                GetUniqueSpecies(reader.GetAttribute(ATTR.species)),
                reader.GetAttribute(ATTR.websearch_status));
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein&gt; tag.
        /// 
        /// In order to support the v0.1 format, the returned node may represent
        /// either a FASTA sequence or a peptide list.
        /// </summary>
        private PeptideGroupData ReadProteinXml(XmlReader reader, PeptideProcessor peptideProcessor)
        {
            string name = reader.GetAttribute(ATTR.name);
            string description = reader.GetAttribute(ATTR.description);
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            var labelProteinMetadata = ReadProteinMetadataXML(reader, true);  // read label_name, label_description, and species, gene etc if any

            reader.ReadStartElement();

            var annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.protein);

            ProteinMetadata[] alternatives;
            if (!reader.IsStartElement(EL.alternatives) || reader.IsEmptyElement)
                alternatives = new ProteinMetadata[0];
            else
            {
                reader.ReadStartElement();
                alternatives = ReadAltProteinListXml(reader);
                reader.ReadEndElement();
            }

            reader.ReadStartElement(EL.sequence);
            string sequence = DecodeProteinSequence(reader.ReadContentAsString());
            reader.ReadEndElement();

            // Support v0.1 documents, where peptide lists were saved as proteins,
            // pre-v0.1 documents, which may not have identified peptide lists correctly.
            if (sequence.StartsWith(@"X") && sequence.EndsWith(@"X"))
                peptideList = true;

            // All v0.1 peptide lists should have a settable label
            if (peptideList)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeName(name ?? string.Empty);
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(description);
            }
            // Or any protein without a name attribute
            else if (name != null)
            {
                labelProteinMetadata = labelProteinMetadata.ChangeDescription(null);
            }

            PeptideGroup group;
            if (peptideList)
                group = new PeptideGroup();
            // If there is no name attribute, ignore all info from the FASTA header line,
            // since it should be user settable.
            else if (name == null)
                group = new FastaSequence(null, null, null, sequence);
            else
                group = new FastaSequence(name, description, alternatives, sequence);

            var peptideGroupData = new PeptideGroupData(group)
            {
                Annotations = annotations,
                AutoManageChildren = autoManageChildren,
                ProteinMetadata = labelProteinMetadata
            };
            if (peptideProcessor != null)
            {
                if (!reader.IsStartElement(EL.selected_peptides))
                    ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
                    reader.ReadEndElement();
                }
            }

            reader.ReadEndElement();

            return peptideGroupData;
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> from a
        /// <see cref="XmlReader"/> positioned at a &lt;protein_group&gt; tag.
        /// </summary>
        private PeptideGroupData ReadProteinGroupXml(XmlReader reader, PeptideProcessor peptideProcessor)
        {
            string name = reader.GetAttribute(ATTR.name);
            string labelName = reader.GetAttribute(ATTR.label_name);
            string labelDescription = reader.GetAttribute(ATTR.label_description);
            bool peptideList = reader.GetBoolAttribute(ATTR.peptide_list);
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);

            reader.ReadStartElement();

            var annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.protein);

            var proteinMetadataList = new List<ProteinMetadata>();
            var proteins = ReadProteinGroupProteinsXml(reader, proteinMetadataList);
            var proteinGroupMetadata = new ProteinGroupMetadata(proteinMetadataList);

            PeptideGroup group;
            if (peptideList)
                group = new PeptideGroup();
            else
                group = new FastaSequenceGroup(name, proteins);

            var peptideGroupData = new PeptideGroupData(group)
            {
                Annotations = annotations,
                ProteinMetadata = proteinGroupMetadata,
                AutoManageChildren = autoManageChildren
            };
            if (!reader.IsStartElement(EL.selected_peptides))
                ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
            else if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement(EL.selected_peptides);
                ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
                reader.ReadEndElement();
            }

            reader.ReadEndElement();

            return peptideGroupData;
        }

        private IList<FastaSequence> ReadProteinGroupProteinsXml(XmlReader reader, List<ProteinMetadata> proteinMetadata)
        {
            var list = new List<FastaSequence>();
            while (reader.IsStartElement(EL.protein))
            {
                var proteinDocNode = ReadProteinXml(reader, null);
                proteinMetadata.Add(proteinDocNode.MakePeptideGroupDocNode().ProteinMetadata);
                list.Add(proteinDocNode.PeptideGroup as FastaSequence);
            }
            return list;
        }

        /// <summary>
        /// Deserializes an array of <see cref="ProteinMetadata"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        /// <param name="reader">The reader positioned at the first element</param>
        /// <returns>A new array of <see cref="ProteinMetadata"/></returns>
        private ProteinMetadata[] ReadAltProteinListXml(XmlReader reader)
        {
            var list = new List<ProteinMetadata>();
            while (reader.IsStartElement(EL.alternative_protein))
            {
                var proteinMetaData = ReadProteinMetadataXML(reader, false);
                reader.Read();
                list.Add(proteinMetaData);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Decodes a FASTA sequence as stored in a XML document to one
        /// with all white space removed.
        /// </summary>
        /// <param name="sequence">The XML format sequence</param>
        /// <returns>The sequence suitible for use in a <see cref="FastaSequence"/></returns>
        private static string DecodeProteinSequence(IEnumerable<char> sequence)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char aa in sequence)
            {
                if (!char.IsWhiteSpace(aa))
                    sb.Append(aa);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a single <see cref="PeptideGroupDocNode"/> representing
        /// a peptide list from a <see cref="XmlReader"/> positioned at the
        /// start element.
        /// </summary>
        private PeptideGroupData ReadPeptideGroupXml(XmlReader reader, PeptideProcessor peptideProcessor)
        {
            ProteinMetadata proteinMetadata = ReadProteinMetadataXML(reader, true); // read label_name and label_description
            bool autoManageChildren = reader.GetBoolAttribute(ATTR.auto_manage_children, true);
            bool isDecoy = reader.GetBoolAttribute(ATTR.decoy);
            var proportionDecoysMatch = reader.GetNullableDoubleAttribute(ATTR.decoy_match_proportion);

            PeptideGroup group = new PeptideGroup(isDecoy);

            var peptideGroupData = new PeptideGroupData(group)
            {
                ProteinMetadata = proteinMetadata,
                AutoManageChildren = autoManageChildren,
                ProportionDecoysMatch = proportionDecoysMatch
            };

            if (reader.IsEmptyElement)
                reader.Read();
            else
            {
                reader.ReadStartElement();
                peptideGroupData.Annotations = ReadTargetAnnotations(reader, AnnotationDef.AnnotationTarget.protein);

                if (!reader.IsStartElement(EL.selected_peptides))
                    ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
                else if (reader.IsEmptyElement)
                    reader.Read();
                else
                {
                    reader.ReadStartElement(EL.selected_peptides);
                    ReadPeptideListXml(reader, peptideGroupData, peptideProcessor);
                    reader.ReadEndElement();
                }

                reader.ReadEndElement();    // peptide_list
            }

            return peptideGroupData;
        }

        /// <summary>
        /// Asynchronously deserializes an array of <see cref="PeptideDocNode"/> objects from
        /// a <see cref="XmlReader"/> positioned at the first element in the list.
        /// </summary>
        private void ReadPeptideListXml(XmlReader reader, PeptideGroupData peptideGroupData, PeptideProcessor queue)
        {
            int order = 0;
            while (reader.IsStartElement(EL.molecule) || reader.IsStartElement(EL.peptide))
            {
                queue.Enqueue(new PeptideWorkItem(peptideGroupData, order++, (XElement)XNode.ReadFrom(reader)));
            }
        }






























        /// <summary>
        /// Returns an XmlReader which is positioned at the start of the element.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private static XmlReader CreateReaderFromElement(XElement element)
        {
            var reader = element.CreateReader();
            // Need to advance the reader so that it is in the correct position to 
            // return attribute values.
            reader.Read();
            return reader;
        }

        /// <summary>
        /// Maintains a work queue for reading the XML for the peptides and molecules
        /// </summary>
        public class PeptideProcessor : IDisposable
        {
            private List<Exception> _exceptions = new List<Exception>();
            private int _totalPeptideCount;
            private int _completedPeptideCount;
            private QueueWorker<PeptideWorkItem> _queue;

            public PeptideProcessor(DocumentReader documentReader)
            {
                DocumentReader = documentReader;
                _queue = new QueueWorker<PeptideWorkItem>(null, ConsumePeptideWorkItem);
                _queue.RunAsync(ParallelEx.GetThreadCount(), @"Load Document XML");
            }
            
            public DocumentReader DocumentReader { get; }
            

            /// <summary>
            /// Queue worker method which processes element for one peptide or molecule in the .sky file
            /// </summary>
            /// <param name="peptideWorkItem">The item to be processed</param>
            /// <param name="threadIndex">Ignored</param>
            private void ConsumePeptideWorkItem(PeptideWorkItem peptideWorkItem, int threadIndex)
            {
                try
                {
                    // One per molecule, made here because this is the one thing every thread does
                    // on its own: what it works out about a molecule belongs to that molecule. The
                    // element is what the queue already holds, so nothing is re-parsed to hand it on.
                    var peptideDocNode = new MoleculeReader(DocumentReader).ReadPeptideXml(
                        peptideWorkItem.XElement,
                        peptideWorkItem.PeptideGroupData.PeptideGroup, peptideWorkItem.XElement.Name == EL.molecule);
                    peptideWorkItem.PeptideGroupData.AddPeptide(peptideWorkItem.Order, peptideDocNode);
                }
                catch (Exception ex)
                {
                    lock (this)
                    {
                        _exceptions.Add(ex);
                        Monitor.PulseAll(this);
                    }
                }
                finally
                {
                    lock (this)
                    {
                        _completedPeptideCount++;
                        Monitor.PulseAll(this);
                    }
                }
            }

            public void WaitUntilFinished()
            {
                while (true)
                {
                    lock (this)
                    {
                        CheckForErrors();
                        if (_completedPeptideCount == _totalPeptideCount)
                        {
                            return;
                        }

                        Monitor.Wait(this);
                    }
                }

            }

            public void Enqueue(PeptideWorkItem peptideWorkItem)
            {
                lock (this)
                {
                    _totalPeptideCount++;
                    _queue.Add(peptideWorkItem);
                }
            }

            private void CheckForErrors()
            {
                lock (this)
                {
                    if (_exceptions.Count > 0)
                    {
                        if (_exceptions.Count == 1)
                        {
                            var singleException = _exceptions.Single();
                            throw new AggregateException(singleException.Message, singleException);
                        }
                        throw new AggregateException(_exceptions);
                    }
                }
            }

            public void Dispose()
            {
                _queue.Dispose();
            }
        }

        public class PeptideGroupData
        {
            public PeptideGroupData(PeptideGroup peptideGroup)
            {
                PeptideGroup = peptideGroup;
                Annotations = Annotations.EMPTY;
            }

            private List<KeyValuePair<int, PeptideDocNode>> _peptides =
                new List<KeyValuePair<int, PeptideDocNode>>();
            public PeptideGroup PeptideGroup { get; }
            public Annotations Annotations { get; set; }
            public ProteinMetadata ProteinMetadata { get; set; }
            public bool AutoManageChildren { get; set; }
            public double? ProportionDecoysMatch { get; set; }

            public void AddPeptide(int order, PeptideDocNode peptideDocNode)
            {
                lock (_peptides)
                {
                    _peptides.Add(new KeyValuePair<int, PeptideDocNode>(order, peptideDocNode));
                }
            }

            public PeptideGroupDocNode MakePeptideGroupDocNode()
            {
                return new PeptideGroupDocNode(PeptideGroup, Annotations, ProteinMetadata, GetPeptideDocNodes(),
                    AutoManageChildren, ProportionDecoysMatch);
            }

            public PeptideDocNode[] GetPeptideDocNodes()
            {
                lock (_peptides)
                {
                    return _peptides.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToArray();
                }
            }
        }

        public class PeptideWorkItem
        {
            public PeptideWorkItem(PeptideGroupData peptideGroupData, int order, XElement element)
            {
                PeptideGroupData = peptideGroupData;
                Order = order;
                XElement = element;
            }

            public PeptideGroupData PeptideGroupData { get; }
            public int Order { get; }
            public XElement XElement { get; }

            public void SetPeptideDocNode(PeptideDocNode peptideDocNode)
            {
                PeptideGroupData.AddPeptide(Order, peptideDocNode);
            }
        }
    }
}
