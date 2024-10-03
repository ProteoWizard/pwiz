/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class VolcanoPlotFormattingTest : AbstractFunctionalTestEx
    {
        private static readonly string GROUP_COMPARISON_NAME = "Test_Group_Comparison";
        private bool IsLayoutTest;

        #region Data

        private static readonly ParseInfo[] PARSE_INFOS =
        {
            new ParseInfo(new MatchExpression(string.Empty, new [] { MatchOption.BelowLeftCutoff}), "BelowLeftCutoff"),
            new ParseInfo(new MatchExpression(string.Empty, new [] { MatchOption.BelowLeftCutoff, MatchOption.AbovePValueCutoff}), "BelowLeftCutoff AbovePValueCutoff"),
            new ParseInfo(null, "PeptideSequence PeptideModifiedSequence", typeof(MatchExpression.InvalidMatchOptionException)),
            new ParseInfo(new MatchExpression("HUMAN", new [] { MatchOption.BelowLeftCutoff, MatchOption.ProteinName }), "ProteinName BelowLeftCutoff:HUMAN"),
            new ParseInfo(null, "Test", typeof(MatchExpression.ParseException)),
            new ParseInfo(new MatchExpression(string.Empty, new MatchOption[]{}), null) 
        };

        private static readonly MatchExprInfo[][] MATCH_EXPR_INFOS =
        {
            new[]
            {
                new MatchExprInfo(
                    new MatchExpression(string.Empty,
                        new[] {MatchOption.BelowLeftCutoff, MatchOption.AbovePValueCutoff}),
                    new VolcanoPlotPointsInfo(17, Color.Gold, true, PointSymbol.Circle, PointSize.normal),
                    new List<string>
                    {
                        "VFWIEVALFWR",
                        "SDFQVPCQYSQQLK",
                        "WWGQEITELAQGPGR",
                        "AGDQILAINEINVK",
                        "AGSWQITMK",
                        "FAEDHFAHEATK",
                        "NLAPLVEDVQSK",
                        "CSSLLWAGAAWLR",
                        "ETGLMAFTNLK",
                        "MLSGFIPLKPTVK",
                        "LQTEGDGIYTLNSEK",
                        "SVVDIGLIK",
                        "IAELFSDLEER",
                        "FSISTDYSLK",
                        "EVLPELGIK",
                        "ALYQAEAFVADFK",
                        "IAELFSELDER"
                    }, false),
            },

            new[]
            {
                new MatchExprInfo(new MatchExpression(string.Empty, new[] {MatchOption.BelowPValueCutoff}),
                    new VolcanoPlotPointsInfo(25, Color.Blue, true, PointSymbol.Square, PointSize.large),
                    new List<string>
                    {
                        "LGGEEVSVACK",
                        "GSYNLQDLLAQAK",
                        "TSDQIHFFFAK",
                        "TGTNLMDFLSR",
                        "LMSPEEKPAPAAK",
                        "GTITSIAALDDPK",
                        "CIVDGDDR",
                        "YLMFFACTILVPK",
                        "WVLTVAHCFEGR",
                        "HTNNGMICLTSLLR",
                        "SFSCEVEILEGDK",
                        "ENSSNILDNLLSR",
                        "TACVLPAPAGPSQGK",
                        "ALIHCLHMS",
                        "ISAEWGEFIK",
                        "DVNEAIQWMEEK",
                        "HFLIETGPK",
                        "SLVIQKPSEENAPK",
                        "AFMDCCNYITK",
                        "MSPVPDLVPGSFK",
                        "MHPELGSFYDSR",
                        "EENGDFASFR",
                        "VTSAAFPSPIEK",
                        "IQELVSGLK",
                        "VVLSGSDATLAYSAFK"
                    }, false),
                new MatchExprInfo(new MatchExpression(string.Empty, new[] {MatchOption.BelowLeftCutoff}),
                    new VolcanoPlotPointsInfo(17, Color.Red, true, PointSymbol.Triangle, PointSize.small),
                    new List<string>
                    {
                        "VFWIEVALFWR",
                        "SDFQVPCQYSQQLK",
                        "WWGQEITELAQGPGR",
                        "AGDQILAINEINVK",
                        "AGSWQITMK",
                        "FAEDHFAHEATK",
                        "NLAPLVEDVQSK",
                        "CSSLLWAGAAWLR",
                        "ETGLMAFTNLK",
                        "MLSGFIPLKPTVK",
                        "LQTEGDGIYTLNSEK",
                        "SVVDIGLIK",
                        "IAELFSDLEER",
                        "FSISTDYSLK",
                        "EVLPELGIK",
                        "ALYQAEAFVADFK",
                        "IAELFSELDER"
                    }, false),
            },

            new[]
            {
                new MatchExprInfo(new MatchExpression("CC|DD", new[] {MatchOption.PeptideSequence}),
                    new VolcanoPlotPointsInfo(4, Color.Green, false, PointSymbol.Diamond, PointSize.x_large),
                    new List<string>
                    {
                        "GTITSIAALDDPK",
                        "CIVDGDDR",
                        "AFMDCCNYITK",
                        "DNCCILDER"
                    }, false),
            },

            new[]
            {
                new MatchExprInfo(new MatchExpression("XP_", new[] {MatchOption.ProteinName}),
                    new VolcanoPlotPointsInfo(6, Color.Yellow, true, PointSymbol.Triangle, PointSize.x_small),
                    new List<string>
                    {
                        "XP_001066264",
                        "XP_001068814",
                        "XP_001057320",
                        "XP_001053003",
                        "XP_001067936",
                        "XP_216782"
                    }, true),
            },

            new[]
            {
                new MatchExprInfo(new MatchExpression("([1-9])\\1+", new[] {MatchOption.ProteinName}),
                    new VolcanoPlotPointsInfo(21, Color.Purple, true, PointSymbol.TriangleDown, PointSize.x_large),
                    new List<string>
                    {
                        "NP_036629",
                        "NP_037244",
                        "NP_444180",
                        "NP_872279",
                        "NP_001121161",
                        "NP_446290",
                        "NP_001101333",
                        "NP_062212",
                        "NP_036774",
                        "NP_001011908",
                        "NP_758823",
                        "NP_036620",
                        "XP_001066264",
                        "XP_001068814",
                        "NP_036664",
                        "NP_113692",
                        "NP_036691",
                        "NP_445770",
                        "NP_062242",
                        "NP_665722",
                        "NP_001033064",
                        "NP_872280"
                    }, true),
            }
        };

        private static PointF[] savedLabelPositions =
        {
            new PointF(0.5285394f, 1.7447474f),
            new PointF(-5.375425f, 6.0f),
            new PointF(-1.12300766f, 6f),
            new PointF(-1.2205646f, 4.17426062f),
            new PointF(-1.07151949f, 3.96921921f),
            new PointF(-1.0508579f, 2.45434284f)
        };
 
        #endregion

        [TestMethod]
        public void TestVolcanoPlotParsing()
        {
            foreach (var parseInfo in PARSE_INFOS)
                parseInfo.AssertCorrect();
        }

        [TestMethod]
        public void TestVolcanoPlotFormatting()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestVolcanoPlotLayout()
        {
            TestFilesZip = "TestFunctional/VolcanoPlotLayoutTest.zip";
            IsLayoutTest = true;
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");

            if (IsLayoutTest)
            {
                var foldChangeForm = FormUtil.OpenForms.OfType<FoldChangeVolcanoPlot>().FirstOrDefault();
                var gridForm = FormUtil.OpenForms.OfType<FoldChangeGrid>().FirstOrDefault();
                Assert.IsNotNull(foldChangeForm);
                WaitForConditionUI(() => foldChangeForm.LabelLayout != null);
                Assert.IsNotNull(foldChangeForm.LabelLayout);
                var labels = foldChangeForm.LabelLayout.LabeledPoints.Values;
                Assert.IsFalse(labels.Any(l => !savedLabelPositions.Any(s => s.Equals(l.LabelPosition))));

                return;
            }

            // Create new group comparison
            var def = CreateGroupComparison(GROUP_COMPARISON_NAME, "Condition", "Healthy", "Diseased");
            Assert.IsNotNull(def);

            var grid = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow(GROUP_COMPARISON_NAME));
            var volcanoPlot = ShowDialog<FoldChangeVolcanoPlot>(() => grid.ShowVolcanoPlot());

            for (var i = 0; i < MATCH_EXPR_INFOS.Length; ++i)
            {
                var matchExprInfo = MATCH_EXPR_INFOS[i];
                var perProtein = matchExprInfo[0].PerProtein;

                if (FindGroupComparison(GROUP_COMPARISON_NAME).PerProtein != perProtein)
                    SetVolcanoPlotPerProtein(volcanoPlot, perProtein);

                WaitForVolcanoPlotPointCount(grid, perProtein ? 48 : 125);
                    
                VerifyMatchExpressions(volcanoPlot, matchExprInfo, 0, i % 2 == 0 ? RemoveMode.Cancel : RemoveMode.Undo); // Alternate remove mode
            }
            TestMatchExpressionListDlg(volcanoPlot);
        }

        private void SetVolcanoPlotPerProtein(Control owner, bool perProtein)
        {
            ChangeGroupComparison(owner, GROUP_COMPARISON_NAME, d =>
            {
                d.RadioScopePerProtein.Checked = perProtein;
                d.RadioScopePerPeptide.Checked = !perProtein;
            });
        }

        public enum RemoveMode
        {
            Cancel,
            Undo
        }

        public class MatchExprInfo
        {
            public MatchExprInfo(MatchExpression matchExpression, VolcanoPlotPointsInfo expectedPointsInfo, List<string> expectedMatches, bool perProtein)
            {
                MatchExpression = matchExpression;
                ExpectedPointsInfo = expectedPointsInfo;
                ExpectedMatches = expectedMatches;
                PerProtein = perProtein;
            }

            public MatchExpression MatchExpression { get; private set; }
            public VolcanoPlotPointsInfo ExpectedPointsInfo { get; private set; }
            public List<string> ExpectedMatches { get; private set; }
            public bool PerProtein { get; private set; }
        }

        private void VerifyMatchExpressions(FoldChangeVolcanoPlot volcanoPlot, MatchExprInfo[] matchExprInfos, int initialRowCount = 0, RemoveMode removeMode = RemoveMode.Cancel)
        {
            int count = matchExprInfos.Length;

            var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(volcanoPlot.ShowFormattingDialog);

            for (var i = 0; i < count; ++i)
            {
                var index = i;
                var exprInfo = matchExprInfos[i];

                var createExprDlg = ShowDialog<CreateMatchExpressionDlg>(() =>
                {
                    var bindingList = formattingDlg.GetCurrentBindingList();
                    Assert.AreEqual(initialRowCount + index, bindingList.Count);

                    bindingList.Add(new MatchRgbHexColor(string.Empty, exprInfo.ExpectedPointsInfo.Labeled,
                        exprInfo.ExpectedPointsInfo.Color, exprInfo.ExpectedPointsInfo.PointSymbol,
                        exprInfo.ExpectedPointsInfo.PointSize));
                    formattingDlg.ClickCreateExpression(bindingList.Count - 1);
                });

                RunUI(() =>
                {
                    createExprDlg.MatchSelectedItem =
                        CreateMatchExpressionDlg.GetMatchOptionStringPair(createExprDlg.MatchItems.ToArray(),
                            exprInfo.MatchExpression.matchOptions);

                    createExprDlg.FoldChangeSelectedItem =
                        CreateMatchExpressionDlg.GetMatchOptionStringPair(createExprDlg.FoldChangeItems.ToArray(),
                            exprInfo.MatchExpression.matchOptions);

                    createExprDlg.PValueSelectedItem =
                        CreateMatchExpressionDlg.GetMatchOptionStringPair(createExprDlg.PValueItems.ToArray(),
                            exprInfo.MatchExpression.matchOptions);

                    createExprDlg.Expression = exprInfo.MatchExpression.RegExpr;
                });

                WaitForConditionUI(() => exprInfo.ExpectedMatches.Count == createExprDlg.MatchingRows.Count(), () =>
                    string.Format("Expected: {0}\nActual: {1}", string.Join(", ", exprInfo.ExpectedMatches),
                        string.Join(", ", createExprDlg.MatchingRows)));

                RunUI(() => CollectionAssert.AreEqual(exprInfo.ExpectedMatches, createExprDlg.MatchingRows.ToArray()));

                OkDialog(createExprDlg, createExprDlg.OkDialog);
            }
            AssertVolcanoPlotCorrect(volcanoPlot, matchExprInfos.Select(info => info.ExpectedPointsInfo).ToArray());

            switch (removeMode)
            {
                case RemoveMode.Cancel:
                    OkDialog(formattingDlg, formattingDlg.CancelDialog);
                    break;
                case RemoveMode.Undo:
                    OkDialog(formattingDlg, formattingDlg.OkDialog);
                    RunUI(SkylineWindow.Undo);
                    break;
                default:
                    Assert.Fail();
                    break;
            }

            var groupDef = FindGroupComparison(GROUP_COMPARISON_NAME);
            Assert.AreEqual(initialRowCount, groupDef.ColorRows.Count);
        }

        public class VolcanoPlotPointsInfo
        {
            public VolcanoPlotPointsInfo(int pointCount, Color color, bool labeled, PointSymbol pointSymbol, PointSize pointSize)
            {
                PointCount = pointCount;
                Color = color;
                Labeled = labeled;
                PointSymbol = pointSymbol;
                PointSize = pointSize;
            }

            public int PointCount { get; private set; }
            public Color Color { get; private set; }
            public bool Labeled { get; private set; }
            public PointSymbol PointSymbol { get; private set; }
            public PointSize PointSize { get; private set; }
        }

        private void AssertVolcanoPlotCorrect(FoldChangeVolcanoPlot plot, VolcanoPlotPointsInfo[] pointsInfos)
        {
            RunUI(() =>
            {
                var curveList = plot.CurveList;
                var startIndex = plot.MatchedPointsStartIndex;
                var matchedCount = curveList.Count - startIndex - 1; // -1 because of the "other" curve item at the end of the curvelist
                Assert.AreEqual(matchedCount, pointsInfos.Length);

                var labelIndex = 0;
                var remainingObjs = plot.GraphObjList.OfType<TextObj>().Count();

                var unselectedStartIndex = plot.LabeledPoints.FindIndex(p => !p.IsSelected);

                if (unselectedStartIndex < 0)
                    remainingObjs = 0;
                else
                    remainingObjs -= unselectedStartIndex;
                

                for (var i = startIndex; i < curveList.Count - 1; ++i)
                {
                    var curveItem = curveList[i];
                    Assert.IsInstanceOfType(curveItem, typeof(LineItem));
                    var lineItem = (LineItem) curveItem;
                    var pointInfo = pointsInfos[i - startIndex];

                    Assert.AreEqual(pointInfo.Color, lineItem.Symbol.Fill.Color);
                    Assert.AreEqual(pointInfo.PointCount, curveItem.Points.Count);
                    Assert.AreEqual(DotPlotUtil.PointSymbolToSymbolType(pointInfo.PointSymbol), lineItem.Symbol.Type);
                    Assert.AreEqual(DotPlotUtil.PointSizeToFloat(pointInfo.PointSize), lineItem.Symbol.Size);

                    if (pointInfo.Labeled)
                    {
                        Assert.IsTrue(remainingObjs >= curveItem.Points.Count);

                        for (var j = 0; j < curveItem.Points.Count; ++j)
                        {
                            var labeledPoint = plot.LabeledPoints[unselectedStartIndex + labelIndex + j];
                            var pointPair = curveItem.Points[j];
                            var graphObj = plot.GraphObjList[unselectedStartIndex + labelIndex + j];
                            Assert.IsInstanceOfType(graphObj, typeof(TextObj));
                            var label = (TextObj) graphObj;

                            Assert.AreEqual(DotPlotUtil.PointSizeToFloat(pointInfo.PointSize), label.FontSpec.Size);
                            // With automated label layout label's coordinates do not match the point coordinates.
                            //Assert.AreEqual(label.Location.X, pointPair.X);
                            //Assert.AreEqual(label.Location.Y, pointPair.Y);

                            Assert.AreEqual(labeledPoint.Label, label);
                            Assert.AreEqual(labeledPoint.Point, pointPair);
                            Assert.IsFalse(labeledPoint.IsSelected);

                            --remainingObjs;
                        }

                        labelIndex += curveItem.Points.Count;
                    }
                }

                Assert.AreEqual(0, remainingObjs);
            });
        }

        private void TestMatchExpressionListDlg(FoldChangeVolcanoPlot volcanoPlot)
        {
            var exprInfo = MATCH_EXPR_INFOS[0][0];
            var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(volcanoPlot.ShowFormattingDialog);
            var createExprDlg = ShowDialog<CreateMatchExpressionDlg>(() =>
            {
                var bindingList = formattingDlg.GetCurrentBindingList();
                bindingList.Add(new MatchRgbHexColor(string.Empty, exprInfo.ExpectedPointsInfo.Labeled,
                    exprInfo.ExpectedPointsInfo.Color, exprInfo.ExpectedPointsInfo.PointSymbol,
                    exprInfo.ExpectedPointsInfo.PointSize));
                formattingDlg.ClickCreateExpression(bindingList.Count - 1);
            });
            var matchExprListDlg = ShowDialog<MatchExpressionListDlg>(createExprDlg.ClickEnterList);
            RunUI(() =>
            {
                // Set the match option to "Protein Gene"
                createExprDlg.matchComboBox.SelectedIndex = 4;
            });
            var proteinList = TextUtil.LineSeparate("Aldoc", "Serpinc1");
            RunUI(() =>
            {
                // Verify that typing into the list is parsed to a REGEX
                matchExprListDlg.proteinsTextBox.Text = proteinList;
                Assert.AreEqual(createExprDlg.Expression, "(?i)^Aldoc$|^Serpinc1$");
            });
            // Two proteins should match
            WaitForCreateRowsChange(createExprDlg, 2);

            // Test empty text; expect matches everything
            RunUI(()=>matchExprListDlg.proteinsTextBox.Text = string.Empty);
            WaitForCreateRowsChange(createExprDlg, 48);

            RunUI(() =>
            {
                // Test case insensitivity
                matchExprListDlg.proteinsTextBox.Clear();
                matchExprListDlg.proteinsTextBox.Text = proteinList.ToUpper();
            });
            // Two proteins should match
            WaitForCreateRowsChange(createExprDlg, 2);
            RunUI(() =>
            {
                // Test empty text: which will match everything
                matchExprListDlg.proteinsTextBox.Text = string.Empty;
            });
            // All proteins should match
            WaitForCreateRowsChange(createExprDlg, 48);
        }
        private void WaitForCreateRowsChange(CreateMatchExpressionDlg createDlg, int expectedRows)
        {
            WaitForConditionUI(() => createDlg.MatchingRows.Count() == expectedRows,
                string.Format("Expecting {0} rows", expectedRows));
        }
        public class ParseInfo
        {
            public ParseInfo(MatchExpression expected, string expression, Type exceptionType = null)
            {
                Expected = expected;
                Expression = expression;
                ExceptionType = exceptionType;
            }

            public void AssertCorrect()
            {
                try
                {
                    var parsed = MatchExpression.Parse(Expression);
                    Assert.AreEqual(Expected.RegExpr, parsed.RegExpr);
                    CollectionAssert.AreEqual(Expected.matchOptions, parsed.matchOptions);
                }
                catch (Exception ex)
                {
                    if (ExceptionType == null || ExceptionType != ex.GetType())
                        Assert.Fail();
                }
            }

            public MatchExpression Expected { get; private set; }
            public string Expression { get; private set; }
            public Type ExceptionType { get; private set; }
        }
    }
}