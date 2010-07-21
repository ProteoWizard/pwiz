using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for PasteTest
    /// </summary>
    [TestClass]
    public class PasteTest
    {
        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion


        private static readonly IsotopeLabelType LABEL_TYPE13_C = new IsotopeLabelType("KR 13C", IsotopeLabelType.FirstHeavy);

        private static readonly IsotopeLabelType LABEL_TYPE15_N = new IsotopeLabelType("All 15N", IsotopeLabelType.FirstHeavy);
        private static readonly IsotopeLabelType LABEL_TYPE13_C_SORTORDER_2 = new IsotopeLabelType("KR 13C", IsotopeLabelType.FirstHeavy);
       
        private static readonly List<StaticMod> HEAVY_MODS_13_C = new List<StaticMod>
            {
                new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null),
                new StaticMod("13C R", "R", ModTerminus.C, null, LabelAtoms.C13, null, null),
                new StaticMod("13C V", "V", LabelAtoms.C13).ChangeExplicit(true),
                new StaticMod("13C L", "L", LabelAtoms.C13).ChangeExplicit(true)
            };
        private static readonly List<StaticMod> HEAVY_MODS_15_N = new List<StaticMod>
            {
                new StaticMod("15N", null, null, null, LabelAtoms.N15, null, null),
            };

        private static readonly StaticMod CARB_CYSTEINE = new StaticMod("Carbamidomethyl Cysteine", "C", "C2H3ON");
        private static readonly StaticMod HEAVY13_C_K = new StaticMod("13C K", "K", ModTerminus.C, null, LabelAtoms.C13, null, null);

        [TestMethod]
        public void TestPaste()
        {
            SrmDocument study7Doc = CreateStudy7Doc();
            study7Doc.Settings.UpdateDefaultModifications();
            IdentityPath path, pathRoot = IdentityPath.ROOT;

            SrmDocument yeastDoc = new SrmDocument(SrmSettingsList.GetDefault());
            yeastDoc = yeastDoc.ImportFasta(new StringReader(LibraryLoadTest.TEXT_FASTA_YEAST_LIB), false, pathRoot,
                                            out path);

            // Test pasting into document with same implicit modifications does not create any extra explicit modifications.
            path = study7Doc.GetPathTo((int) SrmDocument.Level.Peptides, 1);
            var peptideDocNode = (PeptideDocNode) study7Doc.FindNode(path);
            peptideDocNode = TestEnsureMods(study7Doc, study7Doc, peptideDocNode, false);

            // Test implicit mods in source document become explicit mods in target document.
            TestEnsureMods(study7Doc, yeastDoc, peptideDocNode, true);

            // Put explicit modifications on peptide in source document matching target implicit modifications.
            var pathYeastPeptide = yeastDoc.GetPathTo((int) SrmDocument.Level.Peptides, 0);
            var peptideYeast = (PeptideDocNode) yeastDoc.FindNode(pathYeastPeptide);
            var heavy13Ck = new TypedExplicitModifications(peptideYeast.Peptide, IsotopeLabelType.heavy,
                                                           new List<ExplicitMod> {new ExplicitMod(9, HEAVY13_C_K)});
            var explicitMods = new ExplicitMods(peptideYeast.Peptide, new List<ExplicitMod>(),
                                                new List<TypedExplicitModifications> {heavy13Ck});
            yeastDoc = yeastDoc.ChangePeptideMods(pathYeastPeptide, explicitMods, new List<StaticMod> {CARB_CYSTEINE},
                                                  new List<StaticMod> {HEAVY13_C_K});
            // Test explicit mods in source document are not used if target document has matching implicit modifications.
            TestEnsureMods(yeastDoc, study7Doc, peptideYeast, false);

            // Add new label type to source document.
            var settingsNewLabelTypes =
                yeastDoc.Settings.ChangePeptideModifications(mods => new PeptideModifications(mods.StaticModifications,
                  new[] { new TypedModifications (LABEL_TYPE13_C, HEAVY_MODS_13_C) }));
            yeastDoc = yeastDoc.ChangeSettings(settingsNewLabelTypes);
            
            // Test pasting into the same document with new label type.
            yeastDoc = CopyPaste(yeastDoc, null, yeastDoc, pathRoot);
            // Check all transition have correct label type references.
            foreach (var peptide in yeastDoc.Peptides)
            {
                foreach (TransitionGroupDocNode transGroup in peptide.Children)
                {
                    IsotopeLabelType labelType = transGroup.TransitionGroup.LabelType;
                    Assert.IsTrue(ReferenceEquals(labelType,
                                                  PeptideMods(yeastDoc).GetModificationsByName(labelType.Name).LabelType));
                }
            }
            // Check new document still serializes correctly.
            AssertEx.Serializable(yeastDoc, AssertEx.DocumentCloned);

            // Test pasting into new document drops label types from source document that are not found in the target document.
            study7Doc = CopyPaste(yeastDoc, null, study7Doc, pathRoot);
            Assert.AreEqual(1, PeptideMods(study7Doc).GetHeavyModifications().Count());

            // Put new explicit modifications on target document, with different sort order than source document. 
            settingsNewLabelTypes =
                study7Doc.Settings.ChangePeptideModifications(mods => new PeptideModifications(mods.StaticModifications,
                   new[]
                       {
                           new TypedModifications (LABEL_TYPE15_N, HEAVY_MODS_15_N),
                           new TypedModifications (LABEL_TYPE13_C_SORTORDER_2, HEAVY_MODS_13_C)
                       }));
            // Test sort order of label types does not interfere with paste.
            study7Doc = study7Doc.ChangeSettings(settingsNewLabelTypes);
            study7Doc = CopyPaste(yeastDoc, null, study7Doc, pathRoot);

            // Check all transition have correct label type references.
            foreach (var peptide in study7Doc.Peptides)
            {
                foreach (TransitionGroupDocNode transGroup in peptide.Children)
                {
                    IsotopeLabelType labelType = transGroup.TransitionGroup.LabelType;
                    Assert.IsTrue(ReferenceEquals(labelType, 
                        PeptideMods(study7Doc).GetModificationsByName(labelType.Name).LabelType));
                }
            }

            // If only specific children are selected, test that only these children get copied
            int transitionCount = study7Doc.TransitionCount;
            IList<DocNode> selNodes = new List<DocNode>();
            for (int i = 0; i < study7Doc.TransitionCount; i += 2)
            {
                selNodes.Add(study7Doc.Transitions.ToArray()[i]);
            }
            study7Doc = CopyPaste(study7Doc, selNodes, study7Doc, pathRoot);
            Assert.AreEqual(transitionCount + selNodes.Count, study7Doc.TransitionCount);

            // After pasting to a peptide list, make sure all children have been updated to point to the correct parent.
            study7Doc = CopyPaste(yeastDoc, new List<DocNode> {yeastDoc.Peptides.ToList()[0]}, study7Doc,
                                  study7Doc.GetPathTo(0, 0));
            Assert.IsTrue(study7Doc.Peptides.ToList()[0].Peptide ==
                          study7Doc.TransitionGroups.ToList()[0].TransitionGroup.Peptide
                          &&
                          study7Doc.TransitionGroups.ToList()[0].TransitionGroup ==
                          study7Doc.Transitions.ToList()[0].Transition.Group);


            // If only specific transition are selected for a peptide, but all transition groups are included, auto manage children should be true.
            selNodes = new List<DocNode>();
            foreach(TransitionGroupDocNode transGroup in study7Doc.TransitionGroups)
            {
                selNodes.Add(transGroup.Children[0]);
            }
            study7Doc = (SrmDocument) study7Doc.ChangeChildren(new List<DocNode>());
            study7Doc = CopyPaste(study7Doc, selNodes, study7Doc, pathRoot);
            foreach(PeptideDocNode peptide in study7Doc.Peptides)
                Assert.IsTrue(peptide.AutoManageChildren);
        }

        private static SrmDocument CopyPaste(SrmDocument sourceDoc, IEnumerable<DocNode> nodes, SrmDocument targetDoc, IdentityPath to)
        {
            if(nodes != null)
                sourceDoc = sourceDoc.RemoveAllBut(nodes);
           
            var stringWriter = new XmlStringWriter();
            using (var writer = new XmlTextWriter(stringWriter) { Formatting = Formatting.Indented })
            {
                XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
                ser.Serialize(writer, sourceDoc);
            }
            IdentityPath newPath;
            targetDoc = targetDoc.ImportDocumentXml(new StringReader(stringWriter.ToString()),
                                                    Settings.Default.StaticModList,
                                                    Settings.Default.HeavyModList,
                                                    to, out newPath, false);
            return targetDoc;
        }

        private static PeptideModifications PeptideMods(SrmDocument doc)
        {
            return doc.Settings.PeptideSettings.Modifications;
        }

        private static PeptideDocNode TestEnsureMods(SrmDocument source, SrmDocument target, PeptideDocNode node, bool hasMods)
        {
            node= node.EnsureMods(PeptideMods(source), PeptideMods(target), Settings.Default.StaticModList, Settings.Default.HeavyModList);
            if (hasMods) 
                Assert.IsTrue(node.HasExplicitMods);
            else 
                Assert.IsTrue(!node.HasExplicitMods);
            return node;
        }

        private static SrmDocument CreateStudy7Doc()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            var stream = typeof(ExplicitModTest).Assembly.GetManifestResourceStream("pwiz.SkylineTest.Study7.sky");
            Assert.IsNotNull(stream);
            Debug.Assert(stream != null);   // Keep ReSharper from warning
            SrmDocument docStudy7 = (SrmDocument)xmlSerializer.Deserialize(stream);
            AssertEx.IsDocumentState(docStudy7, 0, 7, 11, 22, 66);
            return docStudy7;
        }
    }
}
