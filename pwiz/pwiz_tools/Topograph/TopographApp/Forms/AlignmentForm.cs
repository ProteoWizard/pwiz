using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NHibernate.Criterion;
using pwiz.Common.DataAnalysis;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AlignmentForm : WorkspaceForm
    {
        public AlignmentForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PopulateDataFileCombo(comboDataFile1);
            PopulateDataFileCombo(comboDataFile2);
            comboDataFile1.SelectedIndexChanged += comboDataFile_SelectedIndexChanged;
            comboDataFile2.SelectedIndexChanged += comboDataFile_SelectedIndexChanged;
        }

        void comboDataFile_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshUI();
        }

        protected void RefreshUI()
        {
            zedGraphControlEx1.GraphPane.CurveList.Clear();
            zedGraphControlEx1.GraphPane.GraphObjList.Clear();
            var listItem1 = comboDataFile1.SelectedItem as MsDataFileListItem;
            var listItem2 = comboDataFile2.SelectedItem as MsDataFileListItem;
            if (listItem1 == null || listItem2 == null)
            {
                return;
            }

            var dataFile1 = listItem1.MsDataFile;
            var dataFile2 = listItem2.MsDataFile;
            var searchResults1 = GetSearchResults(dataFile1);
            searchResults1.Sort((a,b)=>GetScanIndex(a).CompareTo(GetScanIndex(b)));
            var searchResults2 = GetSearchResults(dataFile2).ToDictionary(s=>s.Peptide, s=>s);
            var xValues = new List<double>();
            var yValues = new List<double>();
            foreach (var searchResult1 in searchResults1)
            {
                DbPeptideSearchResult searchResult2;
                if (!searchResults2.TryGetValue(searchResult1.Peptide, out searchResult2))
                {
                    continue;
                }
                var x = dataFile1.GetTime(GetScanIndex(searchResult1));
                var y = dataFile2.GetTime(GetScanIndex(searchResult2));
                xValues.Add(x);
                yValues.Add(y);
            }
            var curve = zedGraphControlEx1.GraphPane.AddCurve("Points", xValues.ToArray(), yValues.ToArray(), Color.Black);
            curve.Line.IsVisible = false;
            curve.Symbol.Type = SymbolType.Circle;
            curve.Symbol.Size = 1;
            var loessInterpolator = new LoessInterpolator(.1, 0);
            var weights = Enumerable.Repeat(1.0, xValues.Count).ToArray();
            var smoothedPoints = loessInterpolator.Smooth(xValues.ToArray(), yValues.ToArray(), weights);
            var smoothedCurve = zedGraphControlEx1.GraphPane.AddCurve(
                "Smoothed Curve", xValues.ToArray(), smoothedPoints.ToArray(), Color.Black);
            smoothedCurve.Symbol.IsVisible = false;
            zedGraphControlEx1.GraphPane.AxisChange();
            zedGraphControlEx1.Invalidate();
        }

        private int GetScanIndex(DbPeptideSearchResult dbPeptideSearchResult)
        {
            return (dbPeptideSearchResult.FirstDetectedScan + dbPeptideSearchResult.LastDetectedScan)/2;
        }

        private void PopulateDataFileCombo(ComboBox comboBox)
        {
            var oldSelection = comboBox.SelectedItem as MsDataFileListItem;
            comboBox.Items.Clear();
            var msDataFiles = new List<MsDataFile>(Workspace.MsDataFiles.ListChildren());
            msDataFiles.Sort((d1,d2)=>d1.Label.CompareTo(d2.Label));
            foreach (var msDataFile in msDataFiles)
            {
                comboBox.Items.Add(new MsDataFileListItem(msDataFile));
                if (oldSelection != null && oldSelection.MsDataFile.Equals(msDataFile))
                {
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }
            }
        }

        private List<DbPeptideSearchResult> GetSearchResults(MsDataFile msDataFile)
        {
            var result = new List<DbPeptideSearchResult>();
            using (var session = Workspace.OpenSession())
            {
                var criteria = session.CreateCriteria(typeof (DbPeptideSearchResult))
                    .Add(Restrictions.Eq("MsDataFile", session.Load<DbMsDataFile>(msDataFile.Id)));
                criteria.List(result);
            }
            return result;
        }

        public class MsDataFileListItem
        {
            public MsDataFileListItem(MsDataFile msDataFile)
            {
                MsDataFile = msDataFile;
            }
            public MsDataFile MsDataFile { get; private set; }
            public override string ToString()
            {
                return MsDataFile.Label;
            }
        }
    }
}
