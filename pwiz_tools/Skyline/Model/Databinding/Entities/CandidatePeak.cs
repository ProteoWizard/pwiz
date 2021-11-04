using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Databinding.Entities
{

    [InvariantDisplayName(nameof(CandidatePeakGroup))]
    public class CandidatePeakGroup : SkylineObject, ILinkValue
    {
        private PrecursorResult _precursorResult;
        private ChromatogramGroupInfo _chromatogramGroupInfo;
        private int _peakIndex;
        public CandidatePeakGroup(PrecursorResult precursorResult, ChromatogramGroupInfo chromatogramGroupInfo, int peakIndex, PeakGroupScore defaultPeakScores) : base(precursorResult.DataSchema)
        {
            _precursorResult = precursorResult;
            _chromatogramGroupInfo = chromatogramGroupInfo;
            _peakIndex = peakIndex;
            PeakScores = defaultPeakScores;
        }

        private PrecursorResult GetPrecursorResult()
        {
            return _precursorResult;
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupStartTime
        {
            get
            {
                return GetTransitionPeaks().Min(peak => peak.StartTime);
            }
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupEndTime
        {
            get
            {
                return GetTransitionPeaks().Max(peak => peak.EndTime);
            }
        }


        public bool Chosen { get; private set; }

        public void UpdateChosen(PrecursorResult precursorResult)
        {
            Chosen = EqualTimes(precursorResult.MinStartTime, PeakGroupStartTime) && EqualTimes(precursorResult.MaxEndTime, PeakGroupEndTime);
        }

        private bool EqualTimes(double? time1, double? time2)
        {
            if (!time1.HasValue || !time2.HasValue)
            {
                return false;
            }

            return (float) time1 == (float) time2;
        }

        private IEnumerable<ChromPeak> GetTransitionPeaks()
        {
            return _chromatogramGroupInfo.GetPeakGroup(_peakIndex);
        }

        public override string ToString()
        {
            return string.Format(@"[{0}-{1}]", 
                PeakGroupStartTime.ToString(Formats.RETENTION_TIME),
                PeakGroupEndTime.ToString(Formats.RETENTION_TIME));
        }

        public void LinkValueOnClick(object sender, EventArgs args)
        {
            var skylineWindow = DataSchema.SkylineWindow;
            if (null == skylineWindow)
            {
                return;
            }

            var precursorResult = GetPrecursorResult();
            precursorResult.LinkValueOnClick(sender, args);
            var chromatogramGraph = skylineWindow.GetGraphChrom(precursorResult.GetResultFile().Replicate.Name);
            if (chromatogramGraph != null)
            {
                chromatogramGraph.ZoomToPeak(PeakGroupStartTime, PeakGroupEndTime);
            }
        }
        EventHandler ILinkValue.ClickEventHandler
        {
            get
            {
                return LinkValueOnClick;
            }
        }

        object ILinkValue.Value => this;

        public PeakGroupScore PeakScores
        {
            get;
        }
    }

    public class PrecursorCandidatePeakGroup : CandidatePeakGroup
    {
        public PrecursorCandidatePeakGroup(PrecursorResult precusrorResult, ChromatogramGroupInfo chromatogramGroup, int peakIndex,
            PrecursorCandidatePeakScores defaultPeakScores) : base(precusrorResult, chromatogramGroup, peakIndex, defaultPeakScores)
        {
        }

        public new PrecursorCandidatePeakScores PeakScores
        {
            get { return (PrecursorCandidatePeakScores) base.PeakScores; }
        }

    }

    public class PrecursorCandidatePeakScores : PeakGroupScore
    {
        public PrecursorCandidatePeakScores(FeatureValues scores, double? modelScore, IDictionary<string, WeightedFeature> weightedFeatures) : base(scores, modelScore, weightedFeatures)
        {
        }

        [ChildDisplayName("Candidate{0}")]
        public new Features Features
        {
            get { return base.Features; }
        }
    }
}
