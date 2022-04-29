using System;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding.Entities
{

    [InvariantDisplayName(nameof(CandidatePeakGroup))]
    public class CandidatePeakGroup : SkylineObject, ILinkValue
    {
        private CandidatePeakGroupData _data;
        private PrecursorResult _precursorResult;
        public CandidatePeakGroup(PrecursorResult precursorResult, CandidatePeakGroupData data) : base(precursorResult.DataSchema)
        {
            _data = data;
            _precursorResult = precursorResult;
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupRetentionTime
        {
            get
            {
                return _data.RetentionTime;
            }
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupStartTime
        {
            get
            {
                return _data.MinStartTime;
            }
        }

        [Format(Formats.RETENTION_TIME)]
        public double PeakGroupEndTime
        {
            get
            {
                return _data.MaxEndTime;
            }
        }

        public bool Chosen
        {
            get { return _data.Chosen; }
            set
            {
                if (value == Chosen)
                {
                    return;
                }
                var identityPath = _precursorResult.Precursor.IdentityPath;
                var resultFile = _precursorResult.GetResultFile();

                if (!value)
                {
                    ModifyDocument(EditDescription.Message(_precursorResult.GetElementRef(), Resources.CandidatePeakGroup_Chosen_Remove_Peak), doc =>
                        doc.ChangePeak(identityPath, resultFile.Replicate.Name,
                            resultFile.ChromFileInfo.FilePath, null, null, null, UserSet.TRUE, null, false));
                }
                else
                {
                    ModifyDocument(EditDescription.Message(_precursorResult.GetElementRef(), Resources.CandidatePeakGroup_Chosen_Choose_peak), doc =>
                        doc.ChangePeak(identityPath, resultFile.Replicate.Name,
                            resultFile.ChromFileInfo.FilePath, null, PeakGroupRetentionTime, UserSet.TRUE));
                }
            }
        }

        public override string ToString()
        {
            return string.Format(Resources.CandidatePeakGroup_ToString___0___1__, 
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

            var precursorResult = _precursorResult;
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
            get { return _data.Score; }
        }

        public CandidatePeakGroupData GetCandidatePeakGroupData()
        {
            return _data;
        }
    }
}
