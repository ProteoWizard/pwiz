/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY C:\proj\pwiz\pwiz\pwiz_tools\Skyline\Model\Lib\Library.csKIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Irt
{
    public sealed class IrtDbManager : BackgroundLoader
    {
        private readonly Dictionary<string, RCalcIrt> _loadedCalculators =
            new Dictionary<string, RCalcIrt>();

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null)
            {
                return true;
            }
            if (!ReferenceEquals(GetIrtCalculator(document), GetIrtCalculator(previous)))
            {
                return true;
            }
            return false;
        }

        protected override bool IsLoaded(SrmDocument document)
        {
            var calc = GetIrtCalculator(document);
            return calc == null || calc.IsUsable;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var calc = GetIrtCalculator(docCurrent);
            if (calc != null)
                calc = LoadCalculator(container, calc);
            if (calc == null || !ReferenceEquals(document.Id, container.Document.Id))
            {
                // Loading was cancelled or document changed
                EndProcessing(document);
                return false;
            }

            SrmDocument docNew;
            do
            {
                // Change the document to use the loaded calculator.
                docCurrent = container.Document;
                docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptidePrediction(predict =>
                    predict.ChangeRetentionTime(predict.RetentionTime.ChangeCalculator(calc))));
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
            return true;
        }

        private RCalcIrt LoadCalculator(IDocumentContainer container, RCalcIrt calc)
        {
            // TODO: Something better than locking for the entire load
            lock (_loadedCalculators)
            {
                RCalcIrt calcResult;
                if (!_loadedCalculators.TryGetValue(calc.Name, out calcResult))
                {
                    calcResult = (RCalcIrt) calc.Initialize(new LoadMonitor(this, container, calc));
                    if (calcResult != null)
                        _loadedCalculators.Add(calcResult.Name, calcResult);
                }
                return calcResult;
            }
        }

        private static RCalcIrt GetIrtCalculator(SrmDocument document)
        {
            var regressionRT = document.Settings.PeptideSettings.Prediction.RetentionTime;
            if (regressionRT == null)
                return null;
            return regressionRT.Calculator as RCalcIrt;
        }
    }
}
