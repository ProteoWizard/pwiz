using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public sealed class DetectionsGraphController : GraphSummary.IControllerSplit
    {
        private GraphSummary.IControllerSplit _controllerInterface;
        public DetectionsGraphController()
        {
            _controllerInterface = this as GraphSummary.IControllerSplit;
        }

        public static GraphTypeSummary GraphType
        {
            get { return Helpers.ParseEnum(Settings.Default.DetectionGraphType, GraphTypeSummary.invalid); }
            set { Settings.Default.DetectionGraphType = value.ToString(); }
        }

        GraphSummary GraphSummary.IController.GraphSummary { get; set; }

        UniqueList<GraphTypeSummary> GraphSummary.IController.GraphTypes
        {
            get { return Settings.Default.DetectionGraphTypes; }
            set { Settings.Default.DetectionGraphTypes = value; }
        }

        public IFormView FormView { get { return new GraphSummary.DetectionsGraphView(); } }

        string GraphSummary.IController.Text
        {
            get { return "Detection Counts"; }
        }

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
            return false;
        }

        bool GraphSummary.IControllerSplit.IsPeptidePane(SummaryGraphPane pane)
        {
            throw new NotImplementedException();
        }

        bool GraphSummary.IControllerSplit.IsReplicatePane(SummaryGraphPane pane)
        {
            throw new NotImplementedException();
        }

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

        void GraphSummary.IController.OnRatioIndexChanged()
        {
        }

        void GraphSummary.IController.OnResultsIndexChanged()
        {
        }

        void GraphSummary.IController.OnUpdateGraph()
        {
            var pane = _controllerInterface.GraphSummary.GraphPanes.FirstOrDefault();

            switch (_controllerInterface.GraphSummary.Type)
            {
                case GraphTypeSummary.detections:
                    if (!(pane is DetectionsPlotPane))
                        _controllerInterface.GraphSummary.GraphPanes = new[]
                        {
                            new DetectionsPlotPane(_controllerInterface.GraphSummary), 
                        };
                    break;
                case GraphTypeSummary.detections_histogram:
                    throw new NotImplementedException();
                    //if (!(pane is AreaCVHistogram2DGraphPane))
                    //    _controllerInterface.GraphSummary.GraphPanes = new[]
                    //    {
                    //        new AreaCVHistogram2DGraphPane(_controllerInterface.GraphSummary)
                    //    };
                    //break;
            }

            if (!ReferenceEquals(_controllerInterface.GraphSummary.GraphPanes.FirstOrDefault(), pane))
                (pane as IDisposable)?.Dispose();
        }
    }
}
