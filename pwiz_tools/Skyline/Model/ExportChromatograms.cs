/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Exports processed chromatogram data to a .csv file.
    /// </summary>
    public class ChromatogramExporter
    {
        public SrmDocument Document { get; private set; }
	    
        public ChromatogramExporter(SrmDocument document)
	    {
            Document = document;
            _settings = Document.Settings;
            _measuredResults = _settings.MeasuredResults;
            _matchTolerance = (float)_settings.TransitionSettings.Instrument.MzMatchTolerance;
            _chromatogramSets = _measuredResults.Chromatograms;
	    }

        public const char MAIN_SEPARATOR = TextUtil.SEPARATOR_TSV;

        private readonly SrmSettings _settings;
        private readonly MeasuredResults _measuredResults;
        private readonly float _matchTolerance;
        private readonly IList<ChromatogramSet> _chromatogramSets;

        // ReSharper disable NonLocalizedString
        public static readonly string[] FIELD_NAMES =
        {
            "FileName",
            "PeptideModifiedSequence",
            "PrecursorCharge",
            "ProductMz",
            "FragmentIon",
            "ProductCharge",
            "IsotopeLabelType",
            "TotalArea",
            "Times",
            "Intensities"
        };
        // ReSharper restore NonLocalizedString

        /// <summary>
        /// Executes an export for all chromatograms in the document
        /// with file names matching one of the files in filesToExport
        /// writer = location to write the chromatogram data to
        /// longWaitBroker = progress bar (can be null)
        /// filesToExport = file names for which to write chromatograms
        /// cultureInfo = local culture
        /// chromExtractors = list of special chromatogram types to include (base peak, etc)
        /// chromSources = type of ions to include (precursor, product)
        /// </summary>
        public void Export(TextWriter writer,
                           IProgressMonitor longWaitBroker,
                           IList<string> filesToExport,
                           CultureInfo cultureInfo,
                           IList<ChromExtractor> chromExtractors,
                           IList<ChromSource> chromSources)
        {
            int currentReplicates = 0;
            int totalReplicates = _chromatogramSets.Count;
            var status = new ProgressStatus(string.Empty);
            FormatHeader(writer, FIELD_NAMES);
            foreach (var chromatograms in _chromatogramSets)
            {
                if (longWaitBroker != null)
                {
                    int percentComplete = currentReplicates++ * 100 / totalReplicates;
                    if (percentComplete < 100)
                    {
                        longWaitBroker.UpdateProgress(status = status.ChangeMessage(string.Format(Resources.ChromatogramExporter_Export_Exporting_Chromatograms_for__0_,
                                                                                    chromatograms.Name)).ChangePercentComplete(percentComplete));
                    }
                }
                foreach (var extractor in chromExtractors)
                {
                    ChromatogramGroupInfo[] arrayChromSpecial;
                    if (!_measuredResults.TryLoadAllIonsChromatogram(chromatograms, extractor, true, out arrayChromSpecial))
                    {
                        // TODO: need error determination here
                        continue;
                    }
                    foreach (var chromInfo in arrayChromSpecial)
                    {
                        string fileName = chromInfo.FilePath.GetFileName();
                        // Skip the files that have not been selected for export
                        if (!filesToExport.Contains(fileName))
                            continue;
                        IList<float> times = chromInfo.Times;
                        IList<float> intensities = chromInfo.IntensityArray[0];
                        float tic = CalculateTic(times, intensities);
                        string extractorName = GetExtractorName(extractor);
                        string[] fieldArray =
                        {
                            fileName,
                            TextUtil.EXCEL_NA,
                            TextUtil.EXCEL_NA,
                            TextUtil.EXCEL_NA,
                            extractorName,
                            TextUtil.EXCEL_NA,
                            TextUtil.EXCEL_NA,
                            System.Convert.ToString(tic, cultureInfo)
                        };
                        FormatChromLine(writer, fieldArray, times, intensities, cultureInfo);

                    }
                }
                foreach (var peptideNode in Document.Molecules)
                {
                    foreach (TransitionGroupDocNode groupNode in peptideNode.Children)
                        ExportGroupNode(peptideNode, groupNode, chromatograms, filesToExport, chromSources, writer, cultureInfo);
                }
            }
        }

        private void ExportGroupNode(PeptideDocNode peptideNode,
                                     TransitionGroupDocNode groupNode,
                                     ChromatogramSet chromatograms,
                                     ICollection<string> filesToExport,
                                     ICollection<ChromSource> chromSources,
                                     TextWriter writer,
                                     CultureInfo cultureInfo)
        {
            string peptideModifiedSequence = _settings.GetDisplayName(peptideNode);
            int precursorCharge = groupNode.TransitionGroup.PrecursorCharge;
            string labelType = groupNode.TransitionGroup.LabelType.Name;
            var filesInChrom = chromatograms.MSDataFilePaths.Select(path=>path.GetFileName()).ToList();
            // Don't load the chromatogram group if none of its files are being exported
            if (!filesInChrom.Where(filesToExport.Contains).Any())
                return;
            ChromatogramGroupInfo[] arrayChromInfo;
            if (!_measuredResults.TryLoadChromatogram(chromatograms, peptideNode, groupNode,
                                                     _matchTolerance, true, out arrayChromInfo))
            {
                // TODO: Determine if this is a real error or just a missing node for this file
                // If the former throw an exception, if the latter continue
                return;
            }
            if (arrayChromInfo.Length != chromatograms.FileCount)
            {
                throw new InvalidDataException(string.Format(Resources.ChromatogramExporter_ExportGroupNode_One_or_more_missing_chromatograms_at_charge_state__0__of__1_,
                                               precursorCharge, peptideModifiedSequence));
            }
            foreach (var chromGroupInfo in arrayChromInfo)
            {
                string fileName = chromGroupInfo.FilePath.GetFileName();
                // Skip the files that have not been selected for export
                if (!filesToExport.Contains(fileName))
                    continue;
                foreach (var nodeTran in groupNode.Transitions)
                {
                    // TODO: Check a source attribute on the transition chrom info
                    bool isMs1 = nodeTran.Transition.IsPrecursor() && !nodeTran.HasLoss;
                    if (isMs1 && !chromSources.Contains(ChromSource.ms1))
                        continue;
                    if (!isMs1 && !chromSources.Contains(ChromSource.fragment))
                        continue;
                    int productCharge = nodeTran.Transition.Charge;
                    float productMz = (float)nodeTran.Mz;
                    var chromInfo = chromGroupInfo.GetTransitionInfo(productMz, _matchTolerance);
                    // Sometimes a transition in the transition group does not have results for a particular file
                    // If this happens just skip it for that file
                    if (chromInfo == null)
                        continue;
                    IList<float> times = chromInfo.Times;
                    IList<float> intensities = chromInfo.Intensities;
                    if (times.Count != intensities.Count || intensities.Count == 0)
                    {
                        throw new InvalidDataException(string.Format(Resources.ChromatogramExporter_Export_Bad_chromatogram_data_for_charge__0__state_of_peptide__1_,
                                                       precursorCharge, peptideModifiedSequence));
                    }
                    float tic = CalculateTic(times, intensities);
                    string[] fieldArray =
                    {
                        fileName,
                        peptideModifiedSequence,
                        System.Convert.ToString(precursorCharge, cultureInfo),
                        System.Convert.ToString(productMz, cultureInfo),
                        nodeTran.GetFragmentIonName(CultureInfo.InvariantCulture),
                        System.Convert.ToString(productCharge, cultureInfo),
                        labelType,
                        System.Convert.ToString(tic, cultureInfo)
                    };
                    FormatChromLine(writer, fieldArray, times, intensities, cultureInfo);
                }
            }
        }

        private static float CalculateTic(IList<float> times, IList<float> intensities)
        {
            int numberPoints = intensities.Count;
            double tic = 0;
            for (int i = 0; i < numberPoints - 1; ++i)
            {
                // Trapezoid rule (seconds of intensity)
                tic += ((double)times[i + 1] - times[i]) * 60.0 * ((double)intensities[i + 1] + intensities[i]) / 2.0;
            }
            return (float)tic;
        }

        private static void FormatHeader(TextWriter writer, IList<string> fieldNames)
        {
            for (int i = 0; i < fieldNames.Count; ++i)
            {
                if (i > 0)
                    writer.Write(MAIN_SEPARATOR);
                writer.WriteDsvField(fieldNames[i], MAIN_SEPARATOR);
            }
            writer.WriteLine();
        }

        private static void FormatCsvArray(TextWriter writer, IList<float> floatArray, CultureInfo cultureInfo)
        {
            char intensitySeparator = TextUtil.GetCsvSeparator(cultureInfo);
            for (int i = 0; i < floatArray.Count; ++i)
            {
                if (i > 0)
                    writer.Write(intensitySeparator);
                writer.WriteDsvField(System.Convert.ToString(floatArray[i], cultureInfo), intensitySeparator);
            }
        }

        private static void FormatChromLine(TextWriter writer,
                                     IEnumerable<string> fieldArray,
                                     IList<float> times,
                                     IList<float> intensities,
                                     CultureInfo cultureInfo)
        {
            foreach (string field in fieldArray)
            {
                writer.WriteDsvField(field, MAIN_SEPARATOR);
                writer.Write(MAIN_SEPARATOR);
            }
            FormatCsvArray(writer, times, cultureInfo);
            writer.Write(MAIN_SEPARATOR);
            FormatCsvArray(writer, intensities, cultureInfo);
            writer.WriteLine();
        }

        private static string GetExtractorName(ChromExtractor extractor)
        {
            switch (extractor)
            {
                case ChromExtractor.base_peak:
                    return "BasePeak"; // Not L10N
                case ChromExtractor.summed:
                    return "Summed"; // Not L10N
                default:
                    throw new InvalidDataException(Resources.ChromatogramExporter_GetExtractorName_Invalid_extractor_name_);
            }
        }
    }
}