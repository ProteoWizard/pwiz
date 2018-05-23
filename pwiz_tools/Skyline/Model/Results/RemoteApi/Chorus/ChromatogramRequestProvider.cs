/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    /// <summary>
    /// Creates <see cref="ChromatogramRequestDocument"/> for a subset of the peptides in a document.
    /// If there is a <see cref="IRetentionTimeProvider"/> then chromatograms are fetched in two passes,
    /// and two ChromatogramRequestProviders will be created.
    /// </summary>
    public class ChromatogramRequestProvider
    {
        private readonly SrmDocument _srmDocument;
        private readonly ChorusUrl _chorusUrl;
        private readonly bool _firstPass;
        private readonly IRetentionTimePredictor _retentionTimePredictor;
        private readonly ImmutableList<ChromKey> _chromKeys;

        public ChromatogramRequestProvider(SrmDocument srmDocument, ChorusUrl chorusUrl, IRetentionTimePredictor retentionTimePredictor, bool firstPass)
        {
            _srmDocument = srmDocument;
            _chorusUrl = chorusUrl;
            _retentionTimePredictor = retentionTimePredictor;
            _firstPass = firstPass;
            // Create a SpectrumFilter without an IRetentionTimeProvider in order to get the list of ChromKeys that we will eventually provide.
            SpectrumFilter spectrumFilter = new SpectrumFilter(_srmDocument, _chorusUrl, null, 0);
            _chromKeys = ImmutableList.ValueOf(ListChromKeys(GetChromatogramRequestDocument(spectrumFilter)));
        }

        public SrmDocument SrmDocument { get { return _srmDocument; } }
        public ChorusUrl ChorusUrl { get { return _chorusUrl; } }

        public ImmutableList<ChromKey> ChromKeys { get { return _chromKeys; }}

        public ChromatogramRequestDocument GetChromatogramRequest()
        {
            SpectrumFilter spectrumFilter = new SpectrumFilter(_srmDocument, _chorusUrl, null, 0, _firstPass ? null : _retentionTimePredictor);
            return GetChromatogramRequestDocument(spectrumFilter);
        }

        private ChromatogramRequestDocument GetChromatogramRequestDocument(SpectrumFilter spectrumFilter)
        {
            ChromatogramRequestDocument chromatogramRequestDocument = spectrumFilter.ToChromatogramRequestDocument();
            if (null == _retentionTimePredictor)
            {
                return chromatogramRequestDocument;
            }
            var peptidesBySequence = new Dictionary<string, PeptideDocNode>();
            foreach (var peptide in _srmDocument.Molecules)
            {
                peptidesBySequence[peptide.ModifiedTarget.Sequence] = peptide;
            }
            var chromatogramGroups = new List<ChromatogramRequestDocumentChromatogramGroup>();
            foreach (var chromatogramGroup in chromatogramRequestDocument.ChromatogramGroup)
            {
                PeptideDocNode peptideDocNode = null;
                if (!string.IsNullOrEmpty(chromatogramGroup.ModifiedSequence))
                {
                    peptidesBySequence.TryGetValue(chromatogramGroup.ModifiedSequence, out peptideDocNode);
                }
                bool isFirstPassPeptide = peptideDocNode != null &&
                                          _retentionTimePredictor.IsFirstPassPeptide(peptideDocNode);
                if (_firstPass == isFirstPassPeptide)
                {
                    chromatogramGroups.Add(chromatogramGroup);
                }
            }
            chromatogramRequestDocument.ChromatogramGroup = chromatogramGroups.ToArray();
            return chromatogramRequestDocument;
        }
            
        internal static IEnumerable<ChromKey> ListChromKeys(ChromatogramRequestDocument chromatogramRequestDocument)
        {
            foreach (var chromatogramGroup in chromatogramRequestDocument.ChromatogramGroup)
            {
                ChromSource chromSource;
                switch (chromatogramGroup.Source)
                {
                    case GeneratedCode.ChromSource.Ms1:
                        chromSource = ChromSource.ms1;
                        break;
                    case GeneratedCode.ChromSource.Ms2:
                        chromSource = ChromSource.fragment;
                        break;
                    case GeneratedCode.ChromSource.Sim:
                        chromSource = ChromSource.sim;
                        break;
                    default:
                        chromSource = ChromSource.unknown;
                        break;
                }
                ChromExtractor chromExtractor;
                switch (chromatogramGroup.Extractor)
                {
                    case GeneratedCode.ChromExtractor.BasePeak:
                        chromExtractor = ChromExtractor.base_peak;
                        break;
                    default:
                        chromExtractor = ChromExtractor.summed;
                        break;
                }
                foreach (var chromatogram in chromatogramGroup.Chromatogram)
                {
                    yield return new ChromKey(new Target(chromatogramGroup.ModifiedSequence), new SignedMz(chromatogramGroup.PrecursorMz), 
                        null,
                        new SignedMz(chromatogram.ProductMz), 0, chromatogram.MzWindow, chromSource, chromExtractor, false, false,
                        null, null);    // Optional retention and drift times not used in this provider
                }
            }
        }
    }
}
