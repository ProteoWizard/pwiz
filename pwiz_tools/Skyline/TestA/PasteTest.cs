/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for PasteTest
    /// </summary>
    [TestClass]
    public class PasteTest : AbstractUnitTest
    {
        private static readonly StaticMod VAR_MET_OXIDIZED = new StaticMod("Methionine Oxidized", "M", null, true, "O",
          LabelAtoms.None, RelativeRT.Matching, null, null, null);

        private static readonly List<StaticMod> HEAVY_MODS_13_C = new List<StaticMod>
            {
                new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
                new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
                new StaticMod("13C V", "V", null, LabelAtoms.C13).ChangeExplicit(true),
                new StaticMod("13C L", "L", null, LabelAtoms.C13).ChangeExplicit(true)
            };

        private static readonly List<StaticMod> HEAVY_C_K_DIFDEF = new List<StaticMod>
           {
               new StaticMod("13C K", "R", ModTerminus.C, null, LabelAtoms.C13, null, null)
           };

        private SrmDocument _yeastDocReadOnly;
        private SrmDocument _study7DocReadOnly;
        private SrmDocument _yeastDoc;
        private SrmDocument _study7Doc;

        [TestMethod]
        public void TestPaste()
        {
            ClearDefaultModifications();
            _yeastDoc = new SrmDocument(SrmSettingsList.GetDefault().ChangeTransitionInstrument(instrument => instrument.ChangeMaxMz(1600)));
            _yeastDoc = _yeastDoc.ChangeSettings(_yeastDoc.Settings.ChangeTransitionFilter(filter =>
                                                                                           filter.ChangeMeasuredIons(new MeasuredIon[0])));
            IdentityPath path;
            _yeastDocReadOnly = _yeastDoc = _yeastDoc.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST_LIB),
                false, IdentityPath.ROOT, out path);

            _study7DocReadOnly = _study7Doc = CreateStudy7Doc();

            IdentityPath pathRoot = IdentityPath.ROOT;
            
            // Test pasting into document with same implicit modifications does not create any extra explicit modifications.
            var study7EmptyDoc = (SrmDocument) _study7Doc.ChangeChildren(new DocNode[0]);
            var study7PasteDoc = CopyPaste(_study7Doc, null, study7EmptyDoc, pathRoot);
            var arrayPeptides = _study7Doc.Peptides.ToArray();
            var arrayPastePeptides = study7PasteDoc.Peptides.ToArray();
            Assert.AreEqual(arrayPeptides.Length, arrayPastePeptides.Length);
            AssertEx.DocsEqual(_study7Doc, study7PasteDoc); // DocsEqual gives a more verbose failure message using XML output

            // Test implicit mods in source document become explicit mods in target document.
            ResetDocuments();
            _yeastDoc = (SrmDocument) _yeastDoc.ChangeChildren(new DocNode[0]);
            var settings = _yeastDoc.Settings;
            _yeastDoc = _yeastDoc.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(new StaticMod[0])));
            _yeastDoc = CopyPaste(_study7Doc, null, _yeastDoc, pathRoot);
            var pepMods = _yeastDoc.Settings.PeptideSettings.Modifications;
            Assert.IsTrue(pepMods.StaticModifications != null);
            Assert.IsTrue(pepMods.HasHeavyModifications);
            Assert.IsFalse(pepMods.StaticModifications.Contains(mod => !mod.IsExplicit));
            Assert.IsFalse(pepMods.HeavyModifications.Contains(mod => !mod.IsExplicit));
            
            // Test explicit mods are dropped if the target document has matching implicit modifications.
            study7PasteDoc = CopyPaste(_yeastDoc, null, study7EmptyDoc, pathRoot);
            Assert.AreEqual(_study7Doc, study7PasteDoc);

            // Add new label type to source document.
            ResetDocuments();
            const string labelTypeName13C = "heavy 13C";
            var labelType13C = new IsotopeLabelType(labelTypeName13C, IsotopeLabelType.light.SortOrder + 1);
            _yeastDoc = ChangePeptideModifications(_yeastDoc,
                 new[] {new TypedModifications(labelType13C, HEAVY_MODS_13_C)});
            Assert.IsTrue(_yeastDoc.PeptideTransitionGroups.Contains(nodeGroup =>
                Equals(nodeGroup.TransitionGroup.LabelType, labelType13C)));
           // Test pasting into the same document with new label type.
            _yeastDoc = CopyPaste(_yeastDoc, null, _yeastDoc, pathRoot);
            // Check all transition have correct label type references.
            Assert.IsFalse(_yeastDoc.PeptideTransitionGroups.Contains(nodeGroup =>
                {
                    IsotopeLabelType labelType = nodeGroup.TransitionGroup.LabelType;
                    return !ReferenceEquals(labelType,
                        _yeastDoc.Settings.PeptideSettings.Modifications.GetModificationsByName(labelType.Name).LabelType);
                }));

            // Check new document still serializes correctly.
            AssertEx.Serializable(_yeastDoc, AssertEx.DocumentCloned);

            // Test pasting into new document drops label types from source document that are not found in the target document.
            _yeastDoc = CopyPaste(_yeastDoc, null, new SrmDocument(SrmSettingsList.GetDefault()), pathRoot);
            Assert.IsNull(_yeastDoc.Settings.PeptideSettings.Modifications.GetModificationsByName(labelTypeName13C));

            // If only specific children are selected, test that only these children get copied.
            ResetDocuments();
            var arrayTrans = _study7Doc.PeptideTransitions.ToArray();
            IList<DocNode> selNodes = new List<DocNode>();
            for (int i = 0; i < arrayTrans.Length; i += 2)
            {
                selNodes.Add(arrayTrans[i]);
            }
            _study7Doc = CopyPaste(_study7Doc, selNodes, _study7Doc, pathRoot);
            Assert.AreEqual(arrayTrans.Length + selNodes.Count, _study7Doc.PeptideTransitionCount);

            // Test after pasting to a peptide list, all children have been updated to point to the correct parent.
            ResetDocuments();
            _study7Doc = CopyPaste(_yeastDoc, new[] { _yeastDoc.Peptides.ToArray()[0] }, _study7Doc,
                                  _study7Doc.GetPathTo((int) SrmDocument.Level.MoleculeGroups, 0));
            Assert.AreEqual(_yeastDoc.Peptides.ToArray()[0].Peptide, _study7Doc.Peptides.ToArray()[0].Peptide);
            Assert.AreEqual(_study7Doc.Peptides.ToArray()[0].Peptide,
                          _study7Doc.PeptideTransitionGroups.ToArray()[0].TransitionGroup.Peptide);
            Assert.AreEqual(_study7Doc.PeptideTransitionGroups.ToArray()[0].TransitionGroup,
                          _study7Doc.PeptideTransitions.ToArray()[0].Transition.Group);

            // If only specific transition are selected for a peptide, but all transition groups are included, test AutoManageChildren is true.
            ResetDocuments();
            selNodes = new List<DocNode>();
            foreach (TransitionGroupDocNode transGroup in _study7Doc.PeptideTransitionGroups)
            {
                selNodes.Add(transGroup.Children[0]);
            }
            // TODO: Fix this and make it pass
//            var emptyDoc = (SrmDocument)_study7Doc.ChangeChildren(new List<DocNode>());
//            _study7Doc = CopyPaste(_study7Doc, selNodes, emptyDoc, pathRoot);
            _study7Doc = (SrmDocument)_study7Doc.ChangeChildren(new List<DocNode>());
            _study7Doc = CopyPaste(_study7Doc, selNodes, _study7Doc, pathRoot);
            foreach (PeptideDocNode peptide in _study7Doc.Peptides)
                Assert.IsTrue(peptide.AutoManageChildren);

            // Test pasting in modifications with the same name as but different definition from existing modifications not allowed.
            ResetDocuments();
            _yeastDoc = ChangePeptideModifications(_yeastDoc, 
                new[] { new TypedModifications(IsotopeLabelType.heavy, HEAVY_C_K_DIFDEF) });
            SetDefaultModifications(_study7Doc);
            AssertEx.ThrowsException<Exception>(() => _yeastDoc.Settings.UpdateDefaultModifications(false));

            // Test variable modifications kept if target document has matching variable modifications turned on.
            ResetDocuments();
            settings = _yeastDoc.Settings;
            var modsDefault = settings.PeptideSettings.Modifications;
            var listVariableMods = new List<StaticMod>(modsDefault.StaticModifications) { VAR_MET_OXIDIZED };
            _yeastDoc = _yeastDoc.ChangeSettings(settings.ChangePeptideModifications(mods =>
                mods.ChangeStaticModifications(listVariableMods.ToArray())));
            // Make sure there is an implicitly modified peptide in the yeast document
            Assert.IsTrue(_yeastDoc.Peptides.Contains(nodePep => nodePep.Peptide.Sequence.Contains("C")));
            Assert.IsTrue(_yeastDoc.Peptides.Contains(nodePep => nodePep.HasVariableMods));
            _yeastDoc = CopyPaste(_yeastDoc, null, _yeastDoc, pathRoot);
            Assert.IsFalse(_yeastDoc.Peptides.Contains(nodePep => nodePep.HasExplicitMods && !nodePep.HasVariableMods));

            // Otherwise the variable modifications become only explicit modifications.
            var yeastDocVar = _yeastDoc;
            _yeastDoc = _yeastDoc.ChangeSettings(_yeastDoc.Settings.ChangePeptideModifications(mods => modsDefault));
            _yeastDoc = CopyPaste(yeastDocVar, null, _yeastDoc, pathRoot);
            Assert.IsFalse(_yeastDoc.Settings.PeptideSettings.Modifications.HasVariableModifications);
            Assert.IsTrue(_yeastDoc.Peptides.Contains(nodePep => nodePep.HasExplicitMods));
            Assert.IsFalse(_yeastDoc.Peptides.Contains(nodePep => nodePep.HasVariableMods));
        }

        private static SrmDocument ChangePeptideModifications(SrmDocument doc, IList<TypedModifications> typedHeavyMods)
        {
            var settingsNew =
                doc.Settings.ChangePeptideModifications(mods => new PeptideModifications(mods.StaticModifications,
                   typedHeavyMods));
            return doc.ChangeSettings(settingsNew);
        }

        private static SrmDocument CopyPaste(SrmDocument sourceDoc, IEnumerable<DocNode> nodes, SrmDocument targetDoc, IdentityPath to)
        {
            SetDefaultModifications(targetDoc);
            if(nodes != null)
                sourceDoc = sourceDoc.RemoveAllBut(nodes);
           
            var stringWriter = new XmlStringWriter();
            using (var writer = new XmlTextWriter(stringWriter) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, sourceDoc);
            }
            IdentityPath newPath, nextAdd;
            targetDoc = targetDoc.ImportDocumentXml(new StringReader(stringWriter.ToString()),
                                                    null,
                                                    MeasuredResults.MergeAction.remove,
                                                    false,
                                                    null,
                                                    Settings.Default.StaticModList,
                                                    Settings.Default.HeavyModList,
                                                    to,
                                                    out newPath,
                                                    out nextAdd,
                                                    false);
            return targetDoc;
        }

        private static void ClearDefaultModifications()
        {
            Settings.Default.StaticModList.Clear();
            Settings.Default.HeavyModList.Clear();            
        }

        private static void SetDefaultModifications(SrmDocument doc)
        {
            ClearDefaultModifications();
            doc.Settings.UpdateDefaultModifications(true);
        }

        private void ResetDocuments()
        {
            _yeastDoc = _yeastDocReadOnly;
            _study7Doc = _study7DocReadOnly;
        }

        private SrmDocument CreateStudy7Doc()
        {
            return ResultsUtil.DeserializeDocument("Study7_0-7.sky", GetType());
        }
    }
}
