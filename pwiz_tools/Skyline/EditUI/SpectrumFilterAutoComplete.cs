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
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.Skyline.EditUI
{
    public class SpectrumFilterAutoComplete : IDisposable, IFilterAutoComplete
    {
        private Dictionary<PropertyPath, AutoCompleteStringCollection> _autoCompleteValues;
        private Dictionary<PropertyPath, AutoCompleteStringCollection> _scannedCvValues;
        private CancellationTokenSource _cancellationTokenSource;
        private IDocumentContainer _documentContainer;
        private bool _disposed;
        public SpectrumFilterAutoComplete(IDocumentContainer documentContainer)
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

        public AutoCompleteStringCollection GetAutoCompleteValues(PropertyPath propertyPath)
        {
            lock (this)
            {
                AutoCompleteStringCollection result = null;
                if (_autoCompleteValues?.TryGetValue(propertyPath, out result) != true)
                {
                    _scannedCvValues?.TryGetValue(propertyPath, out result);
                }
                return result;
            }
        }

        /// <summary>
        /// Adds CV term values learned by reading the data files (see <see cref="SpectrumColumnScanner"/>).
        /// A document imported without a CV filter stored no CV terms, so without these there would be
        /// nothing to suggest for a CV column in exactly the case of writing a first CV filter. Values
        /// stored in the cache take precedence, being taken from every spectrum rather than a sample.
        /// </summary>
        public void SetScannedCvValues(SpectrumColumnScanner.ValuesByAccession valuesByAccession)
        {
            var dictionary = new Dictionary<PropertyPath, AutoCompleteStringCollection>();
            foreach (var entry in valuesByAccession)
            {
                if (entry.Value.Count > 0)
                {
                    dictionary[CvPropertyPath(entry.Key)] = MakeCollection(entry.Value);
                }
            }
            lock (this)
            {
                _scannedCvValues = dictionary;
            }
        }

        private static PropertyPath CvPropertyPath(string accession)
        {
            // The path depends only on the accession, so the name and type given here do not matter.
            return SpectrumClassColumn.CvParam(accession, null, false).PropertyPath;
        }

        private static AutoCompleteStringCollection MakeCollection(IEnumerable<string> values)
        {
            var collection = new AutoCompleteStringCollection();
            collection.AddRange(values.ToArray());
            return collection;
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
            var dictionary = new Dictionary<PropertyPath, AutoCompleteStringCollection>();
            if (spectrumMetadataList != null)
            {
                for (int iColumn = 0; iColumn < spectrumMetadataList.Columns.Count; iColumn++)
                {
                    var autoCompleteStringCollection = new AutoCompleteStringCollection();
                    autoCompleteStringCollection.AddRange(spectrumMetadataList.GetColumnValues(iColumn, spectrumMetadataList.AllRows)
                        .Distinct().Select(v => v?.ToString() ?? string.Empty).ToArray());
                    dictionary.Add(spectrumMetadataList.Columns[iColumn].PropertyPath, autoCompleteStringCollection);
                }

                // The CV/user parameters stored on import (only when the document already filtered on
                // one). Read from the terms directly: a CV column's values are text even when the column
                // is numeric, so they cannot go through the typed column arrays above.
                var cvValues = new SpectrumColumnScanner.ValuesByAccession();
                foreach (var spectrum in spectrumMetadataList.AllSpectra)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    foreach (var term in spectrum.OtherParams)
                    {
                        cvValues.Add(term);
                    }
                }
                foreach (var entry in cvValues)
                {
                    if (entry.Value.Count > 0)
                    {
                        dictionary[CvPropertyPath(entry.Key)] = MakeCollection(entry.Value);
                    }
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
