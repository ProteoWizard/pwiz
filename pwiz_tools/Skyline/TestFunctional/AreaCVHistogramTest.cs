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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Controls.Graphs;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AreaCVHistogramTest : AbstractFunctionalTest
    {
        private class AreaCVGraphDataStatistics
        {
            public AreaCVGraphDataStatistics(int dataCount, int objects, double minMeanArea, double maxMeanArea, int total, double maxCv, double minCv, int maxFrequency, double medianCv, double meanCv, double belowCvCutoff)
            {
                DataCount = dataCount;
                Objects = objects;
                MinMeanArea = minMeanArea;
                MaxMeanArea = maxMeanArea;
                Total = total;
                MaxCV = maxCv;
                MinCV = minCv;
                MaxFrequency = maxFrequency;
                MedianCV = medianCv;
                MeanCV = meanCv;
                BelowCVCutoff = belowCvCutoff;
            }

            public AreaCVGraphDataStatistics(AreaCVGraphData data, int objects)
            {
                DataCount = data.Data.Count;
                Objects = objects;
                MinMeanArea = data.MinMeanArea;
                MaxMeanArea = data.MaxMeanArea;
                Total = data.Total;
                MaxCV = data.MaxCV;
                MinCV = data.MinCV;
                MaxFrequency = data.MaxFrequency;
                MedianCV = data.MedianCV;
                MeanCV = data.MeanCV;
                BelowCVCutoff = data.BelowCVCutoff;
            }

            protected bool Equals(AreaCVGraphDataStatistics other)
            {
                return DataCount == other.DataCount &&
                       Objects == other.Objects &&
                       MinMeanArea.Equals(other.MinMeanArea) &&
                       MaxMeanArea.Equals(other.MaxMeanArea) &&
                       Total == other.Total &&
                       MaxCV.Equals(other.MaxCV) &&
                       MinCV.Equals(other.MinCV) &&
                       MaxFrequency == other.MaxFrequency &&
                       MedianCV.Equals(other.MedianCV) &&
                       MeanCV.Equals(other.MeanCV) &&
                       BelowCVCutoff.Equals(other.BelowCVCutoff);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((AreaCVGraphDataStatistics)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = DataCount;
                    hashCode = (hashCode * 397) ^ Objects.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinMeanArea.GetHashCode();
                    hashCode = (hashCode * 397) ^ MaxMeanArea.GetHashCode();
                    hashCode = (hashCode * 397) ^ Total;
                    hashCode = (hashCode * 397) ^ MaxCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MaxFrequency;
                    hashCode = (hashCode * 397) ^ MedianCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ MeanCV.GetHashCode();
                    hashCode = (hashCode * 397) ^ BelowCVCutoff.GetHashCode();
                    return hashCode;
                }
            }

            public override string ToString()
            {
                return string.Format(@"{0}, {1}, {2:R}, {3:R}, {4}, {5:R}, {6:R}, {7}, {8:R}, {9:R}, {10:R}",
                    DataCount, Objects, MinMeanArea, MaxMeanArea, Total, MaxCV, MinCV, MaxFrequency, MedianCV, MeanCV, BelowCVCutoff);
            }

            public int DataCount { get; private set; }
            public int Objects { get; private set; }
            public double MinMeanArea { get; private set; } // Smallest mean area
            public double MaxMeanArea { get; private set; } // Largest mean area
            public int Total { get; private set; } // Total number of CV's
            public double MaxCV { get; private set; } // Highest CV
            public double MinCV { get; private set; } // Smallest CV
            public int MaxFrequency { get; private set; } // Highest count of CV's
            public double MedianCV { get; private set; } // Median CV
            public double MeanCV { get; private set; } // Mean CV
            public double BelowCVCutoff { get; private set; } // Fraction/Percentage of CV's below cutoff
        }

        private bool RecordData { get { return false; } }


        private static readonly AreaCVGraphDataStatistics[] STATS =
        {
            new AreaCVGraphDataStatistics(68, 69, 0, 0, 125, 1.96, 0.13, 7, 0.48281967365197109, 0.56232, 0.024),
            new AreaCVGraphDataStatistics(46, 47, 0, 0, 125, 1.96, 0.12, 9, 0.48281967365197109, 0.55776, 0.024),
            new AreaCVGraphDataStatistics(65, 73, 0, 0, 1744, 1.54, 0, 219, 0.17382217742991013, 0.20458715596330274, 0.63130733944954132),
            new AreaCVGraphDataStatistics(25, 26, 0, 0, 124, 1.54, 0.02, 20, 0.16298477433620745, 0.1970967741935484, 0.72580645161290325),
            new AreaCVGraphDataStatistics(26, 27, 0, 0, 124, 1.5, 0, 15, 0.1372919480977226, 0.17322580645161287, 0.79032258064516125),
            new AreaCVGraphDataStatistics(25, 26, 0, 0, 124, 1.5, 0, 19, 0.091041128301750027, 0.13629032258064519, 0.83064516129032262),
            new AreaCVGraphDataStatistics(121, 122, 4.05, 7.9, 125, 1.96, 0.13, 2, 0.48281967365197109, 0.56232, 0.024),
            new AreaCVGraphDataStatistics(120, 122, 4.05, 7.9, 125, 1.96, 0.12, 2, 0.48281967365197109, 0.55776, 0.024),
            new AreaCVGraphDataStatistics(782, 782, 3.0500000000000003, 8.1, 1744, 1.54, 0, 12, 0.17382217742991013, 0.2045871559633029, 0.63130733944954132),
            new AreaCVGraphDataStatistics(118, 118, 3.6500000000000004, 7.95, 124, 1.54, 0.02, 3, 0.16298477433620745, 0.19709677419354832, 0.72580645161290325),
            new AreaCVGraphDataStatistics(115, 115, 3.6500000000000004, 7.95, 124, 1.5, 0, 2, 0.1372919480977226, 0.1732258064516129, 0.79032258064516125),
            new AreaCVGraphDataStatistics(104, 104, 3.6500000000000004, 7.95, 124, 1.5, 0, 4, 0.091041128301750027, 0.13629032258064522, 0.83064516129032262),
        };

        private static readonly string[][] HISTOGRAM_FINDRESULTS = 
        {
            new[] { "K.DFATVYVDAVK.D [35, 45]", "14% CV in D102" },
            new[] { "R.ASGIIDTLFQDR.F [181, 192]", "14% CV in D102" },
            new[] { "R.HTNNGMICLTSLLR.I [279, 292]", "14% CV in D102" },
            new[] { "K.ENSSNILDNLLSR.M [802, 814]", "14% CV in D102" },
            new[] { "R.ALIHCLHMS.- [436, 444]", "14% CV in D102" },
            new[] { "R.LTHGDFTWTTK.K [90, 100]", "14% CV in D102" },
            new[] { "K.YGLDLGSLLVR.L [1066, 1076]", "14% CV in D102" },
            new[] { "K.AGSWQITMK.G [258, 266]", "14% CV in D102" },
            new[] { "TLNSINIAVFSK", "14% CV in D102" },
            new[] { "ENEGTYYGPDGR", "14% CV in D102" },
            new[] { "ENLSPPLGECLLER", "14% CV in D102" },
            new[] { "ELLDSYIDGR", "14% CV in D102" },
            new[] { "EEPSADALLPIDCR", "14% CV in D102" },
            new[] { "AIEDYVNEFSAR", "14% CV in D102" },
            new[] { "MHPELGSFYDSR", "14% CV in D102" },
            new[] { "VTSAAFPSPIEK", "14% CV in D102" },
            new[] { "AEQGAYLGPLPYK", "14% CV in D102" },
            new[] { "TDEDVPSGPPR", "14% CV in D102" },
            new[] { "ETGLMAFTNLK", "14% CV in D102" },
            new[] { "LQTEGDGIYTLNSEK", "14% CV in D102" },
        };

        private static readonly string[][] HISTOGRAM2D_FINDRESULTS = 
        {
            new[] { "K.DFATVYVDAVK.D [35, 45]", "14% CV in D102" },
            new[] { "R.ASGIIDTLFQDR.F [181, 192]", "14% CV in D102" },
            new[] { "R.HTNNGMICLTSLLR.I [279, 292]", "14% CV in D102" },
            new[] { "K.ENSSNILDNLLSR.M [802, 814]", "14% CV in D102" },
            new[] { "R.ALIHCLHMS.- [436, 444]", "14% CV in D102" },
            new[] { "R.LTHGDFTWTTK.K [90, 100]", "14% CV in D102" },
            new[] { "K.YGLDLGSLLVR.L [1066, 1076]", "14% CV in D102" },
            new[] { "K.AGSWQITMK.G [258, 266]", "14% CV in D102" },
            new[] { "TLNSINIAVFSK", "14% CV in D102" },
            new[] { "ENEGTYYGPDGR", "14% CV in D102" },
            new[] { "ENLSPPLGECLLER", "14% CV in D102" },
            new[] { "ELLDSYIDGR", "14% CV in D102" },
            new[] { "EEPSADALLPIDCR", "14% CV in D102" },
            new[] { "AIEDYVNEFSAR", "14% CV in D102" },
            new[] { "MHPELGSFYDSR", "14% CV in D102" },
            new[] { "VTSAAFPSPIEK", "14% CV in D102" },
            new[] { "AEQGAYLGPLPYK", "14% CV in D102" },
            new[] { "TDEDVPSGPPR", "14% CV in D102" },
            new[] { "ETGLMAFTNLK", "14% CV in D102" },
            new[] { "LQTEGDGIYTLNSEK", "14% CV in D102" },
        };

        private readonly List<string[][]> RecordedFindResults = new List<string[][]>();

        [TestMethod]
        public void TestAreaCVHistograms()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"Rat_plasma.sky")));
            WaitForDocumentLoaded();

            TestHistogram<AreaCVHistogramGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram, 0);
            TestHistogram<AreaCVHistogram2DGraphPane>(SkylineWindow.ShowPeakAreaCVHistogram2D, 6);

            if (RecordData)
            {
                foreach (var recordedFindResults in RecordedFindResults)
                {
                    foreach (var str in recordedFindResults)
                    {
                        Console.WriteLine(@"new[] { " + string.Join(", ", str.Select(s => "\"" + s + "\"")) + @" },");
                    }
                    Console.WriteLine();
                }
                Assert.Fail("Successfully recorded data");
            }
        }
        
        private void TestHistogram<T>(Action showHistogram, int statsStartIndex) where T : SummaryGraphPane
        {
            RunUI(showHistogram);

            WaitForGraphs();

            // Reset settings
            Settings.Default.AreaCVHistogramBinWidth = 1.0;
            RunUI(() => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets));
            AreaGraphController.GroupByGroup = AreaGraphController.GroupByAnnotation = null;
            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.none));
            OpenAndChangeProperties(f => f.ShowCVCutoff = f.ShowMedianCV = true);

            var graph = SkylineWindow.GraphPeakArea;
            var toolbar = graph.Toolbar as AreaCVToolbar;
            Assert.IsNotNull(toolbar);

            // Test if the toolbar is there and if the displayed data is correct
            T pane;
            Assert.IsTrue(graph.TryGetGraphPane(out pane));
            Assert.IsTrue(pane.HasToolbar);
            AssertDataCorrect(pane, statsStartIndex++);

            // Test if the data is correct after changing the bin width and disabling the show cv cutoff option (which affects GetTotalBars)
            Settings.Default.AreaCVHistogramBinWidth = 2.0;
            OpenAndChangeProperties(f => f.ShowCVCutoff = false);
            AssertDataCorrect(pane, statsStartIndex++);

            // Make sure that there are no bars when the points type is decoys
            RunUI(() => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.decoys));
            OpenAndChangeProperties(f => f.ShowMedianCV = false);
            Assert.IsTrue(pane.GetBoxObjCount() == 0);

            // Make sure the toolbar is displaying the annotations correctly and that grouping by "All" works
            AreaGraphController.GroupByGroup = "SubjectId";
            RunUI(() => SkylineWindow.SetAreaCVPointsType(PointsTypePeakArea.targets));
            WaitForGraphs();

            string[] annotations = null;
            RunUI(() => annotations = toolbar.Annotations.ToArray());

            CollectionAssert.AreEqual(annotations,
                new[]
                {
                    "All",
                    "D102", "D103", "D108", "D138", "D154", "D172", "D196",
                    "H146", "H147", "H148", "H159", "H160", "H161", "H162"
                });
            RunUI(() =>
            {
                Assert.IsTrue(toolbar.GroupsVisible);
                Assert.IsFalse(toolbar.DetectionsVisible);
            });
            AssertDataCorrect(pane, statsStartIndex++);
            WaitForCondition(700, () => GetCache(pane).DataCount == 45);
            
            // Make sure that grouping by an annotation works correctly
            AreaGraphController.GroupByAnnotation = "D102";
            UpdateAndWait(graph);
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that clicking a bar/point opens the find resuls and correctly shows the peptides

            var p = new PointF(16.0f, 10.0f);// Center of 14 CV bar
            p = pane.GeneralTransform(p, CoordType.AxisXYScale);
            var mouseEventArgs = new MouseEventArgs(MouseButtons.Left, 1, (int)p.X, (int)p.Y, 0);
            string[][] items = null;
            RunUI(() =>
            {
                pane.HandleMouseMoveEvent(pane.GraphSummary.GraphControl, mouseEventArgs);
                pane.HandleMouseClick(pane.GraphSummary.GraphControl, mouseEventArgs);
            });

            var form = WaitForOpenForm<FindResultsForm>();
            RunUI(() =>
            {
                items = form.ListView.Items.OfType<ListViewItem>().Select(l => l.SubItems
                    .OfType<ListViewItem.ListViewSubItem>().Where((item, index) => index != 1).Select(i => i.Text)
                    .ToArray()).ToArray();
            });

            if (RecordData)
            {
                RecordedFindResults.Add(items);
            }
            else
            {
                // Verify that the peptides are displayed correctly in the find results window
                string[][] expected = null;
                if (typeof(T) == typeof(AreaCVHistogramGraphPane))
                    expected = HISTOGRAM_FINDRESULTS;
                else if (typeof(T) == typeof(AreaCVHistogram2DGraphPane))
                    expected = HISTOGRAM2D_FINDRESULTS;
                else
                    Assert.Fail("T not a Histogram graph pane");

                CollectionAssert.AreEqual(expected, items, Comparer<string[]>.Create((a, b) =>
                {
                    for (var i = 0; i < Math.Min(a.Length, b.Length); ++i)
                    {
                        if (a[i] != b[i])
                            return string.Compare(a[i], b[i], StringComparison.CurrentCulture);
                    }

                    return a.Length.CompareTo(b.Length);
                }));
            }


            // Verify that global standards normalization works
            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.global_standards));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);

            // Verify that median normalization works
            RunUI(() => SkylineWindow.SetNormalizationMethod(AreaCVNormalizationMethod.medians));
            WaitForGraphs();
            AssertDataCorrect(pane, statsStartIndex++);
        }

        private void OpenAndChangeProperties(Action<AreaCVToolbarProperties> action)
        {
            using (var form = ShowDialog<AreaCVToolbarProperties>(() => SkylineWindow.ShowAreaCVPropertyDlg()))
            {
                RunUI(() =>
                {
                    action(form);
                    form.OK();
                });

                UpdateAndWait(SkylineWindow.GraphPeakArea);
            }
        }

        private void UpdateAndWait(GraphSummary graph)
        {
            RunUI(() => { graph.UpdateUI(); });
            WaitForGraphs();
        }

        private static AreaCVGraphData.AreaCVGraphDataCache GetCache(SummaryGraphPane pane)
        {
            if (pane is AreaCVHistogramGraphPane)
                return ((AreaCVHistogramGraphPane)pane).Cache;
            else if (pane is AreaCVHistogram2DGraphPane)
                return ((AreaCVHistogram2DGraphPane)pane).Cache;
            else
                Assert.Fail("Graph pane is not a histogram/histogram2d graph pane");
            return null;
        }

        private static AreaCVGraphData GetCurrentData(SummaryGraphPane pane)
        {
            if (pane is AreaCVHistogramGraphPane)
                return ((AreaCVHistogramGraphPane)pane).CurrentData;
            else if (pane is AreaCVHistogram2DGraphPane)
                return ((AreaCVHistogram2DGraphPane)pane).CurrentData;
            else
                Assert.Fail("Graph pane is not a histogram/histogram2d graph pane");
            return null;
        }

        private void AssertDataCorrect(SummaryGraphPane pane, int statsIndex)
        {
            AreaCVGraphData data = null;
            WaitForConditionUI(() => (data = GetCurrentData(pane)) != null);
            WaitForGraphs();

            RunUI(() =>
            {
                int objects = 0;
                if (pane is AreaCVHistogramGraphPane)
                    objects = pane.GetBoxObjCount();
                else if (pane is AreaCVHistogram2DGraphPane)
                    objects = pane.GetTotalBars();

                if (!RecordData)
                {
                    Assert.AreEqual(STATS[statsIndex], new AreaCVGraphDataStatistics(data, objects));
                    return;
                }

                Console.WriteLine(
                    @"new AreaCVGraphDataStatistics({0}, {1}, {2:R}, {3:R}, {4}, {5:R}, {6:R}, {7}, {8:R}, {9:R}, {10:R}),",
                    data.Data.Count, objects, data.MinMeanArea, data.MaxMeanArea, data.Total, data.MaxCV, data.MinCV, data.MaxFrequency, data.MedianCV, data.MeanCV, data.BelowCVCutoff);
            });
        }
    }
}