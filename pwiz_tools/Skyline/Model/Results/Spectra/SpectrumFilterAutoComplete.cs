/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumFilterAutoComplete : IDisposable
    {
        private Dictionary<string, AutoCompleteStringCollection> _autoCompleteValues;
        private CancellationTokenSource _cancellationTokenSource;
        private IDocumentContainer _documentContainer;
        private bool _disposed;
        public SpectrumFilterAutoComplete(IDocumentContainer documentContainer, IEnumerable<IdentityPath> transitionGroupIdentityPaths)
        {
            _documentContainer = documentContainer;
            RestartQuery(_documentContainer.Document);
        }

        private void RestartQuery(SrmDocument document)
        {
            CancellationToken cancellationToken;
            lock (this)
            {
                if (_disposed)
                {
                    return;
                }
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                cancellationToken = _cancellationTokenSource.Token;
            }

            ActionUtil.RunAsync(() => PopulateUniqueValuesDict(cancellationToken, document));
        }

        public AutoCompleteStringCollection GetAutoCompleteValues(SpectrumClassColumn column)
        {
            lock (this)
            {
                AutoCompleteStringCollection result = null;
                _autoCompleteValues?.TryGetValue(column.ColumnName, out result);
                return result;
            }
        }

        private void PopulateUniqueValuesDict(CancellationToken cancellationToken, SrmDocument document)
        {
            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                SetUniqueValues(cancellationToken, document, null);
                return;
            }

            var spectrumMetadatas = measuredResults.Chromatograms.SelectMany(c => c.MSDataFilePaths).Distinct()
                .Select(path => measuredResults.GetResultFileMetaData(path))
                .Where(metadata=>null != metadata)
                .SelectMany(metadata => metadata.SpectrumMetadatas);
            var spectrumMetadataList = new SpectrumMetadataList(spectrumMetadatas, SpectrumClassColumn.ALL);
            SetUniqueValues(cancellationToken, document, spectrumMetadataList);
        }

        private void SetUniqueValues(CancellationToken cancellationToken, SrmDocument document,
            SpectrumMetadataList spectrumMetadataList)
        {
            var dictionary = new Dictionary<string, AutoCompleteStringCollection>();
            if (spectrumMetadataList != null)
            {
                for (int iColumn = 0; iColumn < spectrumMetadataList.Columns.Count; iColumn++)
                {
                    var autoCompleteStringCollection = new AutoCompleteStringCollection();
                    autoCompleteStringCollection.AddRange(spectrumMetadataList.GetColumnValues(iColumn, spectrumMetadataList.AllRows)
                        .Distinct().Select(v => v?.ToString() ?? string.Empty).ToArray());
                    dictionary.Add(spectrumMetadataList.Columns[iColumn].ColumnName, autoCompleteStringCollection);
                }
            }
            lock (this)
            {
                if (_disposed || cancellationToken.IsCancellationRequested ||
                    !ReferenceEquals(document, _documentContainer.Document))
                {
                    return;
                }

                _autoCompleteValues = dictionary;
            }
        }

        public void Dispose()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    _cancellationTokenSource?.Cancel();
                    _disposed = true;
                }
            }
        }
    }
}
