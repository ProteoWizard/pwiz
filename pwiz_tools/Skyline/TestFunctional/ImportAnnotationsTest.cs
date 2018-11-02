/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportAnnotationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportAnnotations()
        {
            TestFilesZip = @"TestFunctional\ImportAnnotationsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ImportAnnotationsTest.sky")));
            WaitForDocumentLoaded();
            var annotationAdder = new AnnotationAdder();
            Assert.IsTrue(SkylineWindow.SetDocument(annotationAdder.DefineTestAnnotations(SkylineWindow.Document), SkylineWindow.Document));
            var annotatedDocument = annotationAdder.AddAnnotationTestValues(SkylineWindow.Document);
            var annotationSettings = ExportAnnotationSettings.AllAnnotations(annotatedDocument)
                .ChangeRemoveBlankRows(true);

            var documentAnnotations = new DocumentAnnotations(annotatedDocument);
            string annotationPath = TestFilesDir.GetTestPath("annotations.csv");
            documentAnnotations.WriteAnnotationsToFile(CancellationToken.None, annotationSettings, annotationPath);
            RunUI(()=>SkylineWindow.ImportAnnotations(annotationPath));
            Assert.AreEqual(annotatedDocument, SkylineWindow.Document);
            using (var reader = new StreamReader(annotationPath))
            {
                var lines = reader.ReadToEnd().Split('\n').Where(line=>!string.IsNullOrEmpty(line)).ToArray();
                Assert.AreEqual(annotationAdder.ElementCount + 1, lines.Length);
            }
        }

        /// <summary>
        /// Adds a bunch of annotations to all of the elements in a Document.
        /// </summary>
        private class AnnotationAdder
        {
            private int _counter;
            private int _elementCount;

            public SrmDocument AddAnnotationTestValues(SrmDocument document)
            {
                _counter = 0;
                _elementCount = 0;
                document = document.ChangeSettings(document.Settings.ChangeDataSettings(
                    document.Settings.DataSettings.ChangeAnnotationDefs(ImmutableList.ValueOf(GetTestAnnotations()))));
                var measuredResults = document.MeasuredResults;
                if (measuredResults != null)
                {
                    var chromatograms = measuredResults.Chromatograms.ToArray();
                    for (int i = 0; i < chromatograms.Length; i++)
                    {
                        _elementCount++;
                        chromatograms[i] = chromatograms[i].ChangeAnnotations(
                            AddAnnotations(chromatograms[i].Annotations, AnnotationDef.AnnotationTarget.replicate));
                    }
                    document = document.ChangeMeasuredResults(measuredResults.ChangeChromatograms(chromatograms));
                }
                var proteins = document.MoleculeGroups.ToArray();
                for (int iProtein = 0; iProtein < proteins.Length; iProtein++)
                {
                    var protein = proteins[iProtein];
                    _elementCount++;
                    protein = (PeptideGroupDocNode)protein.ChangeAnnotations(AddAnnotations(protein.Annotations,
                        AnnotationDef.AnnotationTarget.protein));
                    var peptides = protein.Molecules.ToArray();
                    for (int iPeptide = 0; iPeptide < peptides.Length; iPeptide++)
                    {
                        var peptide = peptides[iPeptide];
                        _elementCount++;
                        peptide = (PeptideDocNode)peptide.ChangeAnnotations(AddAnnotations(peptide.Annotations,
                            AnnotationDef.AnnotationTarget.peptide));
                        var precursors = peptide.TransitionGroups.ToArray();
                        for (int iPrecursor = 0; iPrecursor < precursors.Length; iPrecursor++)
                        {
                            var precursor = precursors[iPrecursor];
                            precursor = (TransitionGroupDocNode)precursor.ChangeAnnotations(
                                AddAnnotations(precursor.Annotations,
                                    AnnotationDef.AnnotationTarget.precursor));
                            _elementCount++;
                            var transitions = precursor.Transitions.ToArray();
                            for (int iTransition = 0; iTransition < transitions.Length; iTransition++)
                            {
                                var transition = transitions[iTransition];
                                _elementCount++;
                                transition = (TransitionDocNode)transition.ChangeAnnotations(
                                    AddAnnotations(transition.Annotations,
                                        AnnotationDef.AnnotationTarget.transition));
                                if (transition.Results != null)
                                {
                                    var results = transition.Results.ToArray();
                                    for (int replicateIndex = 0; replicateIndex < results.Length; replicateIndex++)
                                    {
                                        _elementCount+=results[replicateIndex].Count;
                                        results[replicateIndex] = new ChromInfoList<TransitionChromInfo>(
                                            results[replicateIndex]
                                                .Select(chromInfo => chromInfo.ChangeAnnotations(AddAnnotations(
                                                    chromInfo.Annotations,
                                                    AnnotationDef.AnnotationTarget.transition_result))));
                                    }
                                    transition = transition.ChangeResults(new Results<TransitionChromInfo>(results));
                                }
                                transitions[iTransition] = transition;
                            }
                            if (precursor.Results != null)
                            {
                                var results = precursor.Results.ToArray();
                                for (int replicateIndex = 0; replicateIndex < results.Length; replicateIndex++)
                                {
                                    _elementCount+=results[replicateIndex].Count;
                                    results[replicateIndex] = new ChromInfoList<TransitionGroupChromInfo>(
                                        results[replicateIndex].Select(chromInfo =>
                                            chromInfo.ChangeAnnotations(AddAnnotations(chromInfo.Annotations,
                                                AnnotationDef.AnnotationTarget.precursor_result))));
                                }
                                precursor = precursor.ChangeResults(new Results<TransitionGroupChromInfo>(results));
                            }
                            precursor = (TransitionGroupDocNode) precursor.ChangeChildren(transitions);
                            precursors[iPrecursor] = precursor;
                        }
                        peptide = (PeptideDocNode) peptide.ChangeChildren(precursors);
                        peptides[iPeptide] = peptide;
                    }
                    protein = (PeptideGroupDocNode) protein.ChangeChildren(peptides);
                    proteins[iProtein] = protein;
                }
                return (SrmDocument)document.ChangeChildren(proteins);
            }

            public SrmDocument DefineTestAnnotations(SrmDocument document)
            {
                return document.ChangeSettings(document.Settings.ChangeDataSettings(
                    document.Settings.DataSettings.ChangeAnnotationDefs(
                        GetTestAnnotations().ToArray())));
            }
            
            private IEnumerable<AnnotationDef> GetTestAnnotations()
            {
                var allTargets = AnnotationDef.AnnotationTargetSet.OfValues(
                    Enum.GetValues(typeof(AnnotationDef.AnnotationTarget))
                        .Cast<AnnotationDef.AnnotationTarget>());
                var noItems = new string[0];
                yield return new AnnotationDef("Text", allTargets, AnnotationDef.AnnotationType.text, noItems);
                yield return new AnnotationDef("Number", allTargets, AnnotationDef.AnnotationType.number, noItems);
                yield return new AnnotationDef("TrueFalse", allTargets, AnnotationDef.AnnotationType.true_false, noItems);
                foreach (var target in Enum.GetValues(typeof(AnnotationDef.AnnotationTarget)).Cast<AnnotationDef.AnnotationTarget>())
                {
                    yield return new AnnotationDef(GetAnnotationTargetName(target), AnnotationDef.AnnotationTargetSet.Singleton(target), AnnotationDef.AnnotationType.text, noItems);
                }
            }

            private Annotations AddAnnotations(Annotations annotations, AnnotationDef.AnnotationTarget annotationTarget)
            {
                annotations = annotations.ChangeAnnotation("Text", annotationTarget + ":" + _counter++);
                if (annotationTarget != AnnotationDef.AnnotationTarget.replicate)
                {
                    annotations = annotations.ChangeNote("Note" + _counter++);
                }
                annotations =
                    annotations.ChangeAnnotation("Number", (_counter++* .1).ToString(CultureInfo.InvariantCulture));
                if (0 != (_elementCount & 2))
                {
                    _counter++;
                    annotations = annotations.ChangeAnnotation("TrueFalse", "TrueFalse");
                }
                annotations = annotations.ChangeAnnotation(GetAnnotationTargetName(annotationTarget),
                    annotationTarget + ":" + _counter++);
                return annotations;
            }

            private string GetAnnotationTargetName(AnnotationDef.AnnotationTarget annotationTarget)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(AnnotationDef.AnnotationTarget), annotationTarget));
                return annotationTarget.ToString();
            }

            public int ElementCount { get { return _elementCount; } }
        }
    }
}
