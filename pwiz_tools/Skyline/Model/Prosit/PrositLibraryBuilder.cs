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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Util;
using Tensorflow.Serving;

namespace pwiz.Skyline.Model.Prosit
{
    class PrositLibraryBuilder : IiRTCapableLibraryBuilder
    {
        private readonly SrmDocument _document;
        private readonly PredictionService.PredictionServiceClient _prositClient;
        private readonly Func<bool> _replaceLibrary;
        private readonly IList<PeptideDocNode> _peptides;
        private readonly IList<TransitionGroupDocNode> _precursors;
        private readonly int _nce;

        public PrositLibraryBuilder(SrmDocument doc, string name, string outPath, Func<bool> replaceLibrary,
            IrtStandard irtStandard,
            IList<PeptideDocNode> peptides, IList<TransitionGroupDocNode> precursors, int nce)
        {
            _prositClient = PrositPredictionClient.Current;
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
            // Predict fragment intensities
            var ms = PrositIntensityModel.Instance.PredictBatches(_prositClient, progress, _document.Settings,
                _peptides.Zip(_precursors,
                    (pep, prec) => new PeptidePrecursorPair(pep, prec, _nce)).ToArray());

            var specMzInfo = ms.Spectra.Select(m => m.SpecMzInfo).ToList();

            // Predict iRTs for peptides
            var distinctPeps = _peptides.Select(p => (PrositRetentionTimeModel.PeptideDocNodeWrapper) p).Distinct().ToArray();
            var iRTMap = PrositRetentionTimeModel.Instance.PredictBatches(_prositClient, progress, _document.Settings,
                distinctPeps);

            for (var i = 0; i < specMzInfo.Count; ++i)
            {
                if (iRTMap.TryGetValue(ms.Spectra[i].PeptidePrecursorPair.NodePep, out var iRT))
                    specMzInfo[i].RetentionTime = iRT;
            }

            // Build library
            var librarySpectra = SpectrumMzInfo.RemoveDuplicateSpectra(specMzInfo);

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

            // Build the library
            using (var blibDb = BlibDb.CreateBlibDb(LibrarySpec.FilePath))
            {
                var docLibrarySpec = new BiblioSpecLiteSpec(LibrarySpec.Name, LibrarySpec.FilePath);
                BiblioSpecLiteLibrary docLibraryNew = null;
                var docLibrarySpec2 = docLibrarySpec;

                docLibraryNew =
                    blibDb.CreateLibraryFromSpectra(docLibrarySpec2, librarySpectra, LibrarySpec.Name, progress);
                if (docLibraryNew == null)
                    return false;
            }

            return true;
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
