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
using System.Text;
using System.Xml;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Serialization
{
    public class DocumentWriter : DocumentSerializer
    {
        public DocumentWriter(SrmDocument document, SkylineVersion skylineVersion)
        {
            Settings = document.Settings;
            Document = document;
            SkylineVersion = skylineVersion;
            DocumentFormat = skylineVersion.SrmDocumentVersion;
            CompactFormatOption = CompactFormatOption.Effective;
        }

        public SkylineVersion SkylineVersion { get; private set; }
        public SrmDocument Document { get; private set; }
        public CompactFormatOption CompactFormatOption { get; set; }

        /// <summary>
        /// Whether to write out every attribute of the chrom infos, which is how documents have
        /// always been written. Sharing a document sets this, because whoever reads the .sky.zip
        /// afterwards - the Panorama website - reads the numbers rather than working them out from
        /// the chromatograms.
        /// <para>
        /// Otherwise only the columnar results are written: the areas, the retention times and
        /// which candidate peak each peak is. That is not a compact <i>encoding</i> of the same
        /// thing, so <see cref="CompactFormatOption"/>, which chooses whether to use protocol
        /// buffers, does not apply to it and is ignored.
        /// </para>
        /// </summary>
        public bool WriteChromInfos { get; set; } = true;

        public event Action<int> WroteTransitions;

        /// <summary>
        /// Raises <see cref="WroteTransitions"/>, which is how a <see cref="MoleculeWriter"/>
        /// reports the transitions it has written.
        /// </summary>
        internal void OnWroteTransitions(int count)
        {
            if (WroteTransitions != null)
                WroteTransitions(count);
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.format_version, SkylineVersion.SrmDocumentVersion);
            writer.WriteAttribute(ATTR.software_version, SkylineVersion.InvariantVersionName);

            writer.WriteElement(Settings.RemoveUnsupportedFeatures(SkylineVersion.SrmDocumentVersion));
            foreach (PeptideGroupDocNode nodeGroup in Document.Children)
            {
                if (nodeGroup.Id is FastaSequenceGroup &&
                    SkylineVersion.SrmDocumentVersion >= DocumentFormat.PROTEIN_GROUPS)
                    writer.WriteStartElement(EL.protein_group);
                else if (nodeGroup.Id is FastaSequence)
                    writer.WriteStartElement(EL.protein);
                else
                    writer.WriteStartElement(EL.peptide_list);
                WritePeptideGroupXml(writer, nodeGroup);
                writer.WriteEndElement();
            }
        }

        private void WriteProteinMetadataXML(XmlWriter writer, ProteinMetadata proteinMetadata, bool skipNameAndDescription)
        {
            if (!skipNameAndDescription)
            {
                writer.WriteAttributeIfString(ATTR.name, proteinMetadata.Name);
                writer.WriteAttributeIfString(ATTR.description, proteinMetadata.Description);
            }
            writer.WriteAttributeIfString(ATTR.accession, proteinMetadata.Accession);
            writer.WriteAttributeIfString(ATTR.gene, proteinMetadata.Gene);
            writer.WriteAttributeIfString(ATTR.species, proteinMetadata.Species);
            writer.WriteAttributeIfString(ATTR.preferred_name, proteinMetadata.PreferredName);
            writer.WriteAttributeIfString(ATTR.websearch_status, proteinMetadata.WebSearchInfo.ToString());
        }

        /// <summary>
        /// Serializes the contents of a single <see cref="PeptideGroupDocNode"/>
        /// to XML.
        /// </summary>
        /// <param name="writer">The XML writer</param>
        /// <param name="node">The peptide group document node</param>
        private void WritePeptideGroupXml(XmlWriter writer, PeptideGroupDocNode node)
        {
            // save the identity info
            if (node.PeptideGroup.Name != null)
            {
                writer.WriteAttributeString(ATTR.name, node.PeptideGroup.Name);
            }
            if (node.PeptideGroup.Description != null && !(node.PeptideGroup is FastaSequenceGroup))
            {
                writer.WriteAttributeString(ATTR.description, node.PeptideGroup.Description);
            }
            // save any overrides
            if ((node.ProteinMetadataOverrides.Name != null) && !Equals(node.ProteinMetadataOverrides.Name, node.PeptideGroup.Name))
            {
                writer.WriteAttributeString(ATTR.label_name, node.ProteinMetadataOverrides.Name);
            }
            if ((node.ProteinMetadataOverrides.Description != null) && !Equals(node.ProteinMetadataOverrides.Description, node.PeptideGroup.Description))
            {
                writer.WriteAttributeString(ATTR.label_description, node.ProteinMetadataOverrides.Description);
            }
            if (!(node.PeptideGroup is FastaSequenceGroup) || SkylineVersion.SrmDocumentVersion < DocumentFormat.PROTEIN_GROUPS)
                WriteProteinMetadataXML(writer, node.ProteinMetadataOverrides, true); // write the protein metadata, skipping the name and description we already wrote
            writer.WriteAttribute(ATTR.auto_manage_children, node.AutoManageChildren, true);
            writer.WriteAttribute(ATTR.decoy, node.IsDecoy);
            writer.WriteAttributeNullable(ATTR.decoy_match_proportion, node.ProportionDecoysMatch);

            // Write child elements
            WriteAnnotations(writer, node.Annotations);

            Action<FastaSequence> writeFastaSequence = seq =>
            {
                if (seq.Alternatives.Count > 0)
                {
                    writer.WriteStartElement(EL.alternatives);
                    foreach (ProteinMetadata alt in seq.Alternatives)
                    {
                        writer.WriteStartElement(EL.alternative_protein);
                        WriteProteinMetadataXML(writer, alt, false); // don't skip name and description
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }

                writer.WriteStartElement(EL.sequence);
                writer.WriteString(FormatProteinSequence(seq.Sequence));
                writer.WriteEndElement();
            };

            FastaSequenceGroup group = node.PeptideGroup as FastaSequenceGroup;
            if (group != null && SkylineVersion.SrmDocumentVersion >= DocumentFormat.PROTEIN_GROUPS)
            {
                var proteinGroupMetadata = node.ProteinMetadataOverrides.ProteinMetadataList;
                Assume.AreEqual(proteinGroupMetadata.Count, group.FastaSequenceList.Count);
                for (var i = 0; i < group.FastaSequenceList.Count; i++)
                {
                    var seq = group.FastaSequenceList[i];
                    var md = proteinGroupMetadata[i];
                    writer.WriteStartElement(EL.protein);
                    writer.WriteAttributeString(ATTR.name, seq.Name);
                    if (!seq.Description.IsNullOrEmpty())
                        writer.WriteAttributeString(ATTR.description, seq.Description);
                    else if (!md.Description.IsNullOrEmpty())
                        writer.WriteAttributeString(ATTR.description, md.Description);
                    WriteProteinMetadataXML(writer, md, true); // write the protein metadata, skipping the name and description we already wrote
                    writeFastaSequence(seq);
                    writer.WriteEndElement();
                }
            }
            else
            {
                FastaSequence seq = node.PeptideGroup as FastaSequence;
                if (seq != null)
                    writeFastaSequence(seq);
            }

            foreach (PeptideDocNode nodePeptide in node.Children)
            {
                var moleculeWriter = new MoleculeWriter(this, writer, nodePeptide);
                moleculeWriter.WriteXml();
            }
        }

        /// <summary>
        /// Formats a FASTA sequence string for output as XML element content.
        /// </summary>
        /// <param name="sequence">An unformated FASTA sequence string</param>
        /// <returns>A formatted version of the input sequence</returns>
        private static string FormatProteinSequence(string sequence)
        {
            const string lineSeparator = "\r\n        ";

            StringBuilder sb = new StringBuilder();
            if (sequence.Length > 50)
                sb.Append(lineSeparator);
            for (int i = 0; i < sequence.Length; i += 10)
            {
                if (sequence.Length - i <= 10)
                    sb.Append(sequence.Substring(i));
                else
                {
                    sb.Append(sequence.Substring(i, Math.Min(10, sequence.Length - i)));
                    // ReSharper disable once LocalizableElement
                    sb.Append(i % 50 == 40 ? "\r\n        " : @" ");
                }
            }

            return sb.ToString();
        }

        public static void WriteAnnotations(XmlWriter writer, Annotations annotations)
        {
            if (annotations.IsEmpty)
                return;

            if (annotations.Note != null || annotations.ColorIndex > 0)
            {
                if (annotations.ColorIndex == 0)
                    writer.WriteElementString(EL.note, annotations.Note);
                else
                {
                    writer.WriteStartElement(EL.note);
                    writer.WriteAttribute(ATTR.category, annotations.ColorIndex);
                    if (annotations.Note != null)
                    {
                        writer.WriteString(annotations.Note);
                    }
                    writer.WriteEndElement();
                }
            }
            foreach (var entry in annotations.ListAnnotations())
            {
                writer.WriteStartElement(EL.annotation);
                writer.WriteAttribute(ATTR.name, entry.Key);
                writer.WriteString(entry.Value);
                writer.WriteEndElement();
            }
        }
    }
}
