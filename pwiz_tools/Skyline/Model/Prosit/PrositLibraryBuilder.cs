/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using Tensorflow.Serving;

namespace pwiz.Skyline.Model.Prosit
{
    class PrositLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private readonly SrmDocument _document;
        private readonly PredictionService.PredictionServiceClient _prositClient;
        private readonly PrositIntensityModel _intensityModel;
        private readonly PrositRetentionTimeModel _rtModel;
        private readonly Func<bool> _replaceLibrary;
        private readonly IList<PeptideDocNode> _peptides;
        private readonly IList<TransitionGroupDocNode> _precursors;
        private readonly int _nce;

        public PrositLibraryBuilder(SrmDocument doc, string name, string outPath, Func<bool> replaceLibrary,
            IrtStandard irtStandard, IList<PeptideDocNode> peptides, IList<TransitionGroupDocNode> precursors, int nce)
        {
            _prositClient = PrositPredictionClient.Current;
            _intensityModel = PrositIntensityModel.Instance;
            _rtModel = PrositRetentionTimeModel.Instance;
            _peptides = peptides;
            _precursors = precursors;
            _document = doc;
            LibrarySpec = new BiblioSpecLiteSpec(name, outPath); // Needs to be created before building
            _replaceLibrary = replaceLibrary;
            IrtStandard = irtStandard;
            _nce = nce;
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();
            try
            {
                var result = BuildLibraryOrThrow(progress, ref progressStatus);
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                return result;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                return false;
            }
        }

        private bool BuildLibraryOrThrow(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeSegments(0, 5);
            var standardSpectra = new List<SpectrumMzInfo>();

            // First get predictions for iRT standards specified by the user which may or may not be in the document
            if (IrtStandard != null && !ReferenceEquals(IrtStandard, IrtStandard.EMPTY))
            {
                var standardPeptidesToAdd = ReadStandardPeptides(IrtStandard);
                if (standardPeptidesToAdd != null && standardPeptidesToAdd.Count > 0)
                {
                    // Get iRTs
                    var standardIRTMap = _rtModel.Predict(_prositClient, _document.Settings,
                        standardPeptidesToAdd.Select(p => (PrositRetentionTimeModel.PeptideDocNodeWrapper)p.NodePep).ToArray(),
                        CancellationToken.None);

                    // Get spectra
                    var standardMS = _intensityModel.PredictBatches(_prositClient, progress, ref progressStatus, _document.Settings,
                        standardPeptidesToAdd.Select(p => p.WithNCE(_nce)).ToArray(),
                        CancellationToken.None);

                    // Merge iRT and MS2 into SpecMzInfos
                    standardSpectra = standardMS.Spectra.Select(m => m.SpecMzInfo).ToList();
                    for (var i = 0; i < standardSpectra.Count; ++i)
                    {
                        if (standardIRTMap.TryGetValue(standardMS.Spectra[i].PeptidePrecursorNCE.NodePep, out var iRT))
                            standardSpectra[i].RetentionTime = iRT;
                    }
                }
            }

            progressStatus = progressStatus.NextSegment();
            // Predict fragment intensities
            PrositMS2Spectra ms = _intensityModel.PredictBatches(_prositClient, progress, ref progressStatus, _document.Settings,
                _peptides.Zip(_precursors,
                    (pep, prec) =>
                        new PrositIntensityModel.PeptidePrecursorNCE(pep, prec, IsotopeLabelType.light, _nce)).ToArray(),
                CancellationToken.None);
            progressStatus = progressStatus.NextSegment();

            var specMzInfo = ms.Spectra.Select(m => m.SpecMzInfo).ToList();

            // Predict iRTs for peptides
            var distinctModifiedSequences = new HashSet<string>();
            var distinctPeps = new List<PrositRetentionTimeModel.PeptideDocNodeWrapper>();
            foreach (var p in _peptides)
            {
                if (distinctModifiedSequences.Add(p.ModifiedSequence))
                {
                    distinctPeps.Add(new PrositRetentionTimeModel.PeptideDocNodeWrapper(p));
                }
            }
            var iRTMap = _rtModel.PredictBatches(_prositClient, progress, ref progressStatus, _document.Settings,
                distinctPeps, CancellationToken.None);
            progressStatus = progressStatus.NextSegment();

            for (var i = 0; i < specMzInfo.Count; ++i)
            {
                if (iRTMap.TryGetValue(ms.Spectra[i].PeptidePrecursorNCE.NodePep, out var iRT))
                    specMzInfo[i].RetentionTime = iRT;
            }

            // Build library
            var librarySpectra = SpectrumMzInfo.RemoveDuplicateSpectra(standardSpectra.Concat(specMzInfo).ToList());

            // Delete if already exists, no merging with Prosit
            var libraryExists = File.Exists(LibrarySpec.FilePath);
            if (libraryExists)
            {
                var replace = _replaceLibrary();
                if (!replace)
                    return false;
                FileEx.SafeDelete(LibrarySpec.FilePath);
            }

            if (!librarySpectra.Any())
                return true;

            progressStatus = progressStatus.NextSegment().ChangeMessage(Resources.SkylineWindow_SaveDocument_Saving___);
            // Build the library
            using (var blibDb = BlibDb.CreateBlibDb(LibrarySpec.FilePath))
            {
                var docLibrarySpec = new BiblioSpecLiteSpec(LibrarySpec.Name, LibrarySpec.FilePath);
                BiblioSpecLiteLibrary docLibraryNew = null;
                var docLibrarySpec2 = docLibrarySpec;

                docLibraryNew =
                    blibDb.CreateLibraryFromSpectra(docLibrarySpec2, librarySpectra, LibrarySpec.Name, progress, ref progressStatus);
                if (docLibraryNew == null)
                    return false;
            }

            return true;
        }

        public static List<PrositIntensityModel.PeptidePrecursorNCE> ReadStandardPeptides(IrtStandard standard)
        {
            var peps = standard.GetDocument().Peptides.ToList();
            var precs = peps.Select(p => p.TransitionGroups.First());
            /*for (var i = 0; i < peps.Count; i++)
            {
                var modSeq = ModifiedSequence.GetModifiedSequence(docImport.Settings, peps[i], IsotopeLabelType.light);
                peps[i] = peps[i].ChangeExplicitMods(new ExplicitMods(peps[i].Peptide,
                    modSeq.ExplicitMods.Select(m => m.ExplicitMod).ToArray(),
                    new TypedExplicitModifications[0]));
            }*/
            return Enumerable.Zip(peps, precs,
                (pep, prec) => new PrositIntensityModel.PeptidePrecursorNCE(pep, prec)).ToList();
        }

        public LibrarySpec LibrarySpec { get; private set; }

        public string AmbiguousMatchesMessage
        {
            get { return null; }
        }

        public IrtStandard IrtStandard { get; }

        public string BuildCommandArgs
        {
            get { return null; }
        }

        public string BuildOutput
        {
            get { return null; }
        }
    }
}
