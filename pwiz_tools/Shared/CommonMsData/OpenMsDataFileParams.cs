using System;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;

namespace pwiz.CommonMsData
{
    public class OpenMsDataFileParams
    {
        public OpenMsDataFileParams() : this(CancellationToken.None)
        {
        }

        public OpenMsDataFileParams(CancellationToken cancellationToken) : this(cancellationToken, null, null)
        {
        }
        public OpenMsDataFileParams(CancellationToken cancellationToken, IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            CancellationToken = cancellationToken;
            ProgressMonitor = progressMonitor;
            ProgressStatus = progressStatus;
            SimAsSpectra = true;
            IgnoreZeroIntensityPoints = true;
        }
        public CancellationToken CancellationToken { get; }
        public IProgressMonitor ProgressMonitor { get; }
        public IProgressStatus ProgressStatus { get; set; }
        public bool SimAsSpectra { get; set; }
        public bool PreferOnlyMs1 { get; set; }
        public bool CentroidMs1 { get; set; }
        public bool CentroidMs2 { get; set; }
        public bool IgnoreZeroIntensityPoints { get; set; }
        public bool PassEntireDiaPasefFrame { get; set; } // When true, ask for diPASEF frames in a single chunk instead of split by isolation ranges
        public string DownloadPath { get; set; }

        public MsDataFileImpl OpenLocalFile(MsDataFilePath msDataFilePath)
        {
            return OpenLocalFile(msDataFilePath.FilePath, msDataFilePath.SampleIndex, msDataFilePath.LockMassParameters);
        }

        public MsDataFileImpl OpenLocalFile(string path, int sampleIndex, LockMassParameters lockMassParameters)
        {
            return new MsDataFileImpl(path, sampleIndex: Math.Max(sampleIndex, 0), lockmassParameters: lockMassParameters,
                simAsSpectra: SimAsSpectra, requireVendorCentroidedMS1: CentroidMs1,
                requireVendorCentroidedMS2: CentroidMs2, preferOnlyMsLevel: PreferOnlyMs1 ? 1 : 0,
                ignoreZeroIntensityPoints: IgnoreZeroIntensityPoints,
                passEntireDiaPasefFrame: PassEntireDiaPasefFrame);
        }
    }
}
