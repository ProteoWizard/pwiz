using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class CommandLinePeakBoundaryImputer
    {
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public static SrmDocument ImputeBoundaries(SrmDocument document, IProgressMonitor progressMonitor,
            IProgressStatus progressStatus, bool overwriteManual)
        {
            var measuredResults = document.MeasuredResults;
            if (measuredResults == null)
            {
                return document;
            }

            var productionMonitor = new ProductionMonitor(CancellationToken.None, progress =>
            {
                progressStatus = progressStatus.ChangePercentComplete(progress);
                progressMonitor.UpdateProgress(progressStatus);
            });
            progressMonitor.UpdateProgress(progressStatus =
                progressStatus.ChangeMessage("Performing retention time alignment"));
            var chromatogramTimeRanges = measuredResults.GetChromatogramTimeRanges(CancellationToken.None);
            var alignmentParameters = new AlignmentData.Parameters(document);
            var alignmentResults = alignmentParameters.GetAlignmentParameters().GetResults(productionMonitor);
            var alignmentData = new AlignmentData(alignmentParameters, alignmentResults, chromatogramTimeRanges);
            var imputationParameters = new PeakImputationRows.Parameters(document).ChangeOverwriteManualPeaks(overwriteManual);
            progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage("Evaluating peaks"));
            var peakImputationRows =
                PeakImputationRows.ProduceRows(productionMonitor, imputationParameters, alignmentData);
            progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangeMessage("Imputing peak boundaries"));
            var newDocument = peakImputationRows.ImputeBoundaries(productionMonitor, document.BeginDeferSettingsChanges(), null);
            if (newDocument == null)
            {
                return document;
            }

            return newDocument.EndDeferSettingsChanges(document,
                new SrmSettingsChangeMonitor(progressMonitor, "Finishing peak boundary imputation", progressStatus));
        }
    }
}
