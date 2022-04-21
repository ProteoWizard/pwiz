/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed class DetectionsGraphController : GraphSummary.IControllerSplit
    {

        public class IntLabeledValue : LabeledValues<int>
        {
            protected IntLabeledValue(int value, Func<string> getLabelFunc) : base(value, getLabelFunc)
            {
                Value = value;
            }
            public float Value { get; private set; }

            public override string ToString()
            {
                if (this is TargetType)
                    return Label;
                else
                    return string.Format(Label, Settings.TargetType);
            }

            public static IEnumerable<T> GetValues<T>() where T : IntLabeledValue
            {
                return (IEnumerable<T>)typeof(T).InvokeMember("GetValues", BindingFlags.InvokeMethod,
                    null, null, new object[0]);
            }

            public static T GetDefaultValue<T>() where T : IntLabeledValue
            {
                return (T)typeof(T).InvokeMember("GetDefaultValue", BindingFlags.InvokeMethod,
                    null, null, new object[0]);
            }

            public static T GetFromString<T>(string str) where T : IntLabeledValue
            {
                T res;
                if (typeof(T) == typeof(TargetType))
                    res = GetValues<T>().FirstOrDefault(
                        (t) => t.Label.Equals(str));
                else
                    res = GetValues<T>().FirstOrDefault(
                        (t) => string.Format(t.Label, Settings.TargetType).Equals(str));

                if (res == default(T))
                    return GetDefaultValue<T>();
                else return res;
            }

            public static void PopulateCombo<T>(ComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }
            public static void PopulateCombo<T>(ToolStripComboBox comboBox, T currentValue) where T : IntLabeledValue
            {
                comboBox.Items.Clear();
                foreach (var val in GetValues<T>())
                {
                    comboBox.Items.Add(val);
                    if (Equals(val, currentValue))
                    {
                        comboBox.SelectedIndex = comboBox.Items.Count - 1;
                    }
                }
            }

            public static T GetValue<T>(ComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
            public static T GetValue<T>(ToolStripComboBox comboBox, T defaultVal) where T : IntLabeledValue
            {
                return comboBox.SelectedItem as T ?? defaultVal;
            }
        }

        public class TargetType : IntLabeledValue
        {
            private TargetType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc) { }

            public static readonly TargetType PRECURSOR = new TargetType(0, () => Resources.DetectionPlot_TargetType_Precursor);
            public static readonly TargetType PEPTIDE = new TargetType(1, () => Resources.DetectionPlot_TargetType_Peptide);

            public static IEnumerable<TargetType> GetValues()
            {
                return new[] { PRECURSOR, PEPTIDE };
            }

            public static TargetType GetDefaultValue()
            {
                return PRECURSOR;
            }
        }

        public class YScaleFactorType : IntLabeledValue
        {
            private YScaleFactorType(int value, Func<string> getLabelFunc) : base(value, getLabelFunc) { }

            public static readonly YScaleFactorType ONE = new YScaleFactorType(1, () => Resources.DetectionPlot_YScale_One);
            public static readonly YScaleFactorType PERCENT = new YScaleFactorType(0, () => Resources.DetectionPlot_YScale_Percent);

            public static IEnumerable<YScaleFactorType> GetValues()
            {
                return new[] { ONE, PERCENT };
            }
            public static YScaleFactorType GetDefaultValue()
            {
                return ONE;
            }
        }


        public class Settings
        {
            public static float QValueCutoff
            {
                get => Properties.Settings.Default.DetectionsQValueCutoff;
                set => Properties.Settings.Default.DetectionsQValueCutoff = value;
            }

            public static TargetType TargetType
            {
                get => IntLabeledValue.GetFromString<TargetType>(
                    Properties.Settings.Default.DetectionsTargetType);
                set => Properties.Settings.Default.DetectionsTargetType = value.ToString();
            }
            public static YScaleFactorType YScaleFactor
            {
                get => IntLabeledValue.GetFromString<YScaleFactorType>(
                    Properties.Settings.Default.DetectionsYScaleFactor);
                set => Properties.Settings.Default.DetectionsYScaleFactor = value.ToString();
            }

            public static int RepCount
            {
                get => Properties.Settings.Default.DetectionsRepCount;
                set => Properties.Settings.Default.DetectionsRepCount = value;
            }

            public static float FontSize
            {
                get => Properties.Settings.Default.AreaFontSize;
                set => Properties.Settings.Default.AreaFontSize = value;
            }

            public static bool ShowAtLeastN
            {
                get => Properties.Settings.Default.DetectionsShowAtLeastN;
                set => Properties.Settings.Default.DetectionsShowAtLeastN = value;
            }

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static bool ShowSelection
            {
                get => Properties.Settings.Default.DetectionsShowSelection;
                set => Properties.Settings.Default.DetectionsShowSelection = value;
            }

            public static bool ShowMean
            {
                get => Properties.Settings.Default.DetectionsShowMean;
                set => Properties.Settings.Default.DetectionsShowMean = value;
            }
            public static bool ShowLegend
            {
                get => Properties.Settings.Default.DetectionsShowLegend;
                set => Properties.Settings.Default.DetectionsShowLegend = value;
            }
        }


        private GraphSummary.IControllerSplit _controllerInterface;
        public DetectionsGraphController()
        {
            _controllerInterface = this;
        }

        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Properties.Settings.Default.DetectionGraphType, GraphTypeSummary.invalid); }
            set { Properties.Settings.Default.DetectionGraphType = value.ToString(); }
        }

        GraphSummary GraphSummary.IController.GraphSummary { get; set; }

        UniqueList<GraphTypeSummary> GraphSummary.IController.GraphTypes
        {
            get => Properties.Settings.Default.DetectionGraphTypes; 
            set => Properties.Settings.Default.DetectionGraphTypes = value; 
        }

        public IFormView FormView =>new GraphSummary.DetectionsGraphView(); 

        string GraphSummary.IController.Text => Resources.SkylineWindow_CreateGraphDetections_Counts;

        SummaryGraphPane GraphSummary.IControllerSplit.CreatePeptidePane(PaneKey key)
        {
            throw new NotImplementedException();
        }

        SummaryGraphPane GraphSummary.IControllerSplit.CreateReplicatePane(PaneKey key)
        {
            throw new NotImplementedException();
        }

        bool GraphSummary.IController.HandleKeyDownEvent(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Escape)
                DetectionPlotData.GetDataCache().Cancel();
            return true;
        }

        bool GraphSummary.IControllerSplit.IsPeptidePane(SummaryGraphPane pane) => false;

        bool GraphSummary.IControllerSplit.IsReplicatePane(SummaryGraphPane pane) => false;

        void GraphSummary.IController.OnActiveLibraryChanged()
        {
            (this as GraphSummary.IController).GraphSummary.UpdateUI();
        }

        void GraphSummary.IController.OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
            var settingsNew = newDocument.Settings;
            var settingsOld = oldDocument.Settings;

            if (_controllerInterface.GraphSummary.Type == GraphTypeSummary.detections ||
                _controllerInterface.GraphSummary.Type == GraphTypeSummary.detections_histogram)
            {
            }
        }

        void GraphSummary.IController.OnResultsIndexChanged()
        {
            if (_controllerInterface.GraphSummary.GraphPanes.OfType<DetectionsByReplicatePane>().Any())
                _controllerInterface.GraphSummary.UpdateUI();
        }

        void GraphSummary.IController.OnUpdateGraph()
        {
            var pane = _controllerInterface.GraphSummary.GraphPanes.FirstOrDefault();

            switch (_controllerInterface.GraphSummary.Type)
            {
                case GraphTypeSummary.detections:
                    if (!(pane is DetectionsByReplicatePane))
                        _controllerInterface.GraphSummary.GraphPanes = new[]
                        {
                            new DetectionsByReplicatePane(_controllerInterface.GraphSummary), 
                        };
                    break;
                case GraphTypeSummary.detections_histogram:
                    if (!(pane is DetectionsHistogramPane))
                        _controllerInterface.GraphSummary.GraphPanes = new[]
                        {
                            new DetectionsHistogramPane(_controllerInterface.GraphSummary)
                        };
                    break;
            }
        }
    }
}
