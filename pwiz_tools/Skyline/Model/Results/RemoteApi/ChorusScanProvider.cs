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
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class ChorusScanProvider : IScanProvider
    {
        private ChorusSession _chorusSession;
        public ChorusScanProvider(string docFilePath, ChorusUrl chorusUrl, ChromSource source, float[] times,
            TransitionFullScanInfo[] transitions)
        {
            ChorusUrl = chorusUrl;
            DocFilePath = docFilePath;
            DataFilePath = chorusUrl;
            Source = source;
            Times = times;
            Transitions = transitions;
            _chorusSession = new ChorusSession();
        }

        public ChorusUrl ChorusUrl { get; private set; }
        public ChromSource ChromSource { get; private set; }
        public void Dispose()
        {
            _chorusSession.Abort();
        }

        public string DocFilePath { get; private set; }
        public MsDataFileUri DataFilePath { get; private set; }
        public ChromSource Source { get; private set; }
        public float[] Times { get; private set; }
        public TransitionFullScanInfo[] Transitions { get; private set; }
        public MsDataSpectrum[] GetMsDataFileSpectraWithCommonRetentionTime(int dataFileSpectrumStartIndex)
        {
            double precursor = Transitions.Select(transition => transition.PrecursorMz).FirstOrDefault();
            ChorusAccount chorusAccount = ChorusUrl.FindChorusAccount(Settings.Default.ChorusAccountList);
            return _chorusSession.GetSpectra(chorusAccount, ChorusUrl, Source, precursor, dataFileSpectrumStartIndex);
        }

        public bool Adopt(IScanProvider scanProvider)
        {
            return false;
        }
    }
}
