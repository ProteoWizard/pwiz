using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class DiaUmpireDdaConverter : AbstractDdaConverter, IProgressMonitor
    {
        private readonly DiaUmpire.WindowScheme _windowScheme;
        private readonly IEnumerable<DiaUmpire.TargetWindow> _variableWindows;
        private readonly DiaUmpire.Config _diaUmpireConfig;
        private IProgressMonitor _parentProgressMonitor;
        private IProgressStatus _progressStatus;
        private int _currentSourceIndex;

        public DiaUmpireDdaConverter(AbstractDdaSearchEngine searchEngine, IsolationScheme isolationScheme, DiaUmpire.Config diaUmpireConfig) : base(searchEngine)
        {
            var isolationWindows = isolationScheme.PrespecifiedIsolationWindows;
            var windowSizes = isolationWindows.Skip(1).Select(w => Math.Round(w.End - w.Start, 1));
            bool fixedSizeWindows = windowSizes.Distinct().Count() == 1;

            _windowScheme = DiaUmpire.WindowScheme.SWATH_Variable;
            if (fixedSizeWindows)
                _windowScheme = DiaUmpire.WindowScheme.SWATH_Fixed;

            _variableWindows = isolationWindows.Select(w => new DiaUmpire.TargetWindow { Start = w.Start, End = w.End });
            _diaUmpireConfig = diaUmpireConfig;
            _parentProgressMonitor = null;
        }

        public override bool Run(IProgressMonitor progressMonitor, IProgressStatus status)
        {
            _parentProgressMonitor = progressMonitor;
            _progressStatus = status;

            try
            {
                OriginalSpectrumSources = SearchEngine.SpectrumFileNames;
                ConvertedSpectrumSources = new MsDataFileUri[OriginalSpectrumSources.Length];

                progressMonitor?.UpdateProgress(_progressStatus.ChangeMessage(Resources.DiaUmpireDdaConverter_Run_Starting_DIA_Umpire_conversion));

                int sourceIndex = 0;
                foreach (var spectrumSource in OriginalSpectrumSources)
                {
                    _currentSourceIndex = sourceIndex;

                    // TODO/CONSIDER: source path may not be writable
                    string outputFilepath = Path.Combine(Path.GetDirectoryName(spectrumSource.GetFilePath()) ?? "",
                        spectrumSource.GetFileNameWithoutExtension() + "-diaumpire.mz5");
                    ConvertedSpectrumSources[sourceIndex] = new MsDataFilePath(outputFilepath);
                    ++sourceIndex;

                    // CONSIDER: read the file description to see what settings were used to generate the file;
                    // if the same settings were used, we can re-use the file, else regenerate
                    /*if (MsDataFileImpl.IsValidFile(outputFilepath))
                    {
                        progressMonitor?.UpdateProgress(status.ChangeMessage($"Re-using existing DiaUmpire file for {spectrumSource.GetSampleOrFileName()}"));
                        continue;
                    }
                    else*/
                    FileEx.SafeDelete(outputFilepath);

                    string tmpFilepath = Path.GetTempFileName();

                    using (var diaUmpire = new DiaUmpire(spectrumSource.GetFilePath(),
                        Math.Max(spectrumSource.GetSampleIndex(), 0),
                        _windowScheme, _variableWindows, _diaUmpireConfig,
                        spectrumSource.GetLockMassParameters(), true,
                        requireVendorCentroidedMS1: true,
                        requireVendorCentroidedMS2: true,
                        progressMonitor: this))
                    {
                        diaUmpire.WriteToFile(tmpFilepath, true);
                    }

                    if (progressMonitor?.IsCanceled == true)
                    {
                        FileEx.SafeDelete(tmpFilepath, true);
                        return false;
                    }

                    File.Move(tmpFilepath, outputFilepath);
                    _progressStatus = _progressStatus.NextSegment();
                }

                // tell the search engine to search the converted files instead of the original files
                SearchEngine.SetSpectrumFiles(ConvertedSpectrumSources);
                return true;
            }
            catch (Exception e)
            {
                progressMonitor?.UpdateProgress(status.ChangeErrorException(e));
                return false;
            }
        }

        public bool IsCanceled => _parentProgressMonitor?.IsCanceled == true;
        public bool HasUI => false;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (_parentProgressMonitor == null)
                throw new InvalidOperationException(@"null _parentProgressMonitor");

            string currentSourceName = OriginalSpectrumSources[_currentSourceIndex].GetSampleOrFileName();
            string sourceSpecificProgress = $@"[{currentSourceName} ({_currentSourceIndex+1} of {OriginalSpectrumSources.Length})] {status.Message}";
            return _parentProgressMonitor.UpdateProgress(status.ChangeMessage(sourceSpecificProgress).ChangeSegments(_currentSourceIndex, OriginalSpectrumSources.Length * 2));
        }
    }
}