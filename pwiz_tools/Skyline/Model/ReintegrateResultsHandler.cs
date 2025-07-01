using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes.PeakImputation;

namespace pwiz.Skyline.Model
{
    public class ReintegrateResultsHandler
    {
        public ReintegrateResultsHandler(MProphetResultsHandler mProphetResultsHandler,
            PeakBoundaryImputer peakBoundaryImputer)
        {
            MProphetResultsHandler = mProphetResultsHandler;
            PeakBoundaryImputer = peakBoundaryImputer;
        }

        public MProphetResultsHandler MProphetResultsHandler { get; }
        public PeakBoundaryImputer PeakBoundaryImputer { get; }
        public bool FreeImmutableMemory
        {
            get { return MProphetResultsHandler.FreeImmutableMemory; }
        }

        public bool OverrideManual
        {
            get
            {
                return MProphetResultsHandler.OverrideManual;
            }
        }

        public bool IncludeDecoys
        {
            get
            {
                return MProphetResultsHandler.IncludeDecoys;
            }
        }

        public double QValueCutoff
        {
            get { return MProphetResultsHandler.QValueCutoff; }
        }

        public PeakFeatureStatistics GetPeakFeatureStatistics(Peptide peptide, ChromFileInfoId fileId)
        {
            return MProphetResultsHandler.GetPeakFeatureStatistics(peptide, fileId);
        }

        public ChromPeak GetBestPeak(PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, 
            ChromatogramSet chromatogramSet, ChromatogramInfo chromatogramInfo,
            ref PeakGroupIntegrator peakGroupIntegrator,
            out UserSet userSet)
        {
            var chromFileInfoId = chromatogramSet.FindFile(chromatogramInfo.FilePath);
            var peakFeatureStatistics = MProphetResultsHandler.GetPeakFeatureStatistics(peptideDocNode.Peptide, chromFileInfoId);
            int bestIndex = chromatogramInfo.BestPeakIndex;
            userSet = UserSet.FALSE;
            if (peakFeatureStatistics != null)
            {
                var qvalue = peakFeatureStatistics.QValue;
                if (qvalue.HasValue && qvalue.Value > QValueCutoff)
                {
                    userSet = UserSet.REINTEGRATED;
                    bestIndex = -1;
                }
                // Otherwise, if the reintegrate peak is different from the default
                // best peak, then use it and mark the peak as chosen by reintegration
                else if (bestIndex != peakFeatureStatistics.BestPeakIndex)
                {
                    userSet = UserSet.REINTEGRATED;
                    bestIndex = peakFeatureStatistics.BestPeakIndex;
                }
            }

            var imputationSettings = PeakBoundaryImputer?.Document.Settings.PeptideSettings.Imputation ?? ImputationSettings.DEFAULT;
            if (Equals(ImputationSettings.DEFAULT, imputationSettings))
            {
                return bestIndex < 0 ? ChromPeak.EMPTY : chromatogramInfo.GetPeak(bestIndex);
            }

            ChromPeak candidatePeak = ChromPeak.EMPTY;
            if (bestIndex >= 0)
            {
                candidatePeak = chromatogramInfo.GetPeak(bestIndex);
                if (!imputationSettings.MaxRtShift.HasValue && !imputationSettings.MaxPeakWidthVariation.HasValue)
                {
                    return candidatePeak;
                }
            }

            PeakBounds candidatePeakBounds = null;
            if (!candidatePeak.IsEmpty)
            {
                candidatePeakBounds = new PeakBounds(candidatePeak.StartTime, candidatePeak.EndTime);
            }
            var imputedPeak = PeakBoundaryImputer!.GetImputedPeak(peptideDocNode, chromatogramSet, chromatogramInfo.FilePath, candidatePeakBounds);
            if (imputedPeak == null)
            {
                return candidatePeak;
            }

            if (Equals(candidatePeakBounds, imputedPeak.PeakBounds))
            {
                return candidatePeak;
            }

            userSet = UserSet.REINTEGRATED;
            var settings = PeakBoundaryImputer.Document.Settings;
            peakGroupIntegrator ??=
                transitionGroupDocNode.MakePeakGroupIntegrator(settings, chromatogramSet,
                    chromatogramInfo.GroupInfo);
            ChromPeak.FlagValues flags = 0;
            if (settings.MeasuredResults.IsTimeNormalArea)
                flags = ChromPeak.FlagValues.time_normalized;
            return chromatogramInfo.CalcPeak(peakGroupIntegrator, (float)imputedPeak.PeakBounds.StartTime,
                (float)imputedPeak.PeakBounds.EndTime, flags);
        }
    }
}
