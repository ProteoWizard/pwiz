/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using Peptide = pwiz.Skyline.Model.Peptide;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MatchExpressionTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMatchExpressionCutoffs()
        {
            var doc = GetTestDocument();
            var dataSchema = SkylineDataSchema.MemoryDataSchema(doc, DataSchemaLocalizer.INVARIANT);
            var matchExpression = new MatchExpression(null, new []{MatchOption.BelowLeftCutoff, MatchOption.AbovePValueCutoff});
            var cutoffSettings = new FoldChangeVolcanoPlot.PropertiesCutoffSettings();
            var protein = new Protein(dataSchema, doc.GetPathTo((int)SrmDocument.Level.MoleculeGroups, 0));
            var peptide = protein.Peptides.First();
            Settings.Default.Log2FoldChangeCutoff = 1;
            Settings.Default.PValueCutoff = -Math.Log10(.05);
            Assert.IsTrue(matchExpression.Matches(doc, protein, peptide,
                new FoldChangeResult(0, .01, new LinearFitResult(-3), 0), cutoffSettings));
            Assert.IsFalse(matchExpression.Matches(doc, protein, peptide,
                new FoldChangeResult(0, .01, new LinearFitResult(3), 0), cutoffSettings));
            Assert.IsFalse(matchExpression.Matches(doc, protein, peptide,
                new FoldChangeResult(0, .06, new LinearFitResult(-3), 0), cutoffSettings));
            Assert.IsFalse(matchExpression.Matches(doc, protein, peptide,
                new FoldChangeResult(0, .06, new LinearFitResult(3), 0), cutoffSettings));
        }

        private SrmDocument GetTestDocument()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var peptides = new[]
            {
                new PeptideDocNode(new Peptide("ELVISK"), srmSettings, null, null, ExplicitRetentionTimeInfo.EMPTY,
                    Array.Empty<TransitionGroupDocNode>(), true),
                new PeptideDocNode(new Peptide("LIVESK"), srmSettings, null, null, ExplicitRetentionTimeInfo.EMPTY,
                    Array.Empty<TransitionGroupDocNode>(), true)
            };
            var doc = new SrmDocument(SrmSettingsList.GetDefault());
            doc = (SrmDocument) doc.ChangeChildren(new[]
            {
                new PeptideGroupDocNode(new PeptideGroup(), "Peptide List", null, peptides)
            });
            return doc;
        }
    }
}
