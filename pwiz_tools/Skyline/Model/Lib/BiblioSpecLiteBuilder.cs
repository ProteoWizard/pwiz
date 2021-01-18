/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.BiblioSpec;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public interface IiRTCapableLibraryBuilder : ILibraryBuilder
    {
        string AmbiguousMatchesMessage { get; }
        IrtStandard IrtStandard { get; }
        string BuildCommandArgs { get; }
        string BuildOutput { get; }
    }

    public sealed class BiblioSpecLiteBuilder : IiRTCapableLibraryBuilder
    {
        // ReSharper disable LocalizableElement
        public const string EXT_PEP_XML = ".pep.xml";
        public const string EXT_PEP_XML_ONE_DOT = ".pepXML";
        public const string EXT_MZID = ".mzid";
        public const string EXT_MZID_GZ = ".mzid.gz";
        public const string EXT_IDP_XML = ".idpXML";
        public const string EXT_SQT = ".sqt";
        public const string EXT_DAT = ".dat";
        public const string EXT_SSL = ".ssl";
        public const string EXT_XTAN_XML = ".xtan.xml";
        public const string EXT_PROTEOME_DISC = ".msf";
        public const string EXT_PROTEOME_DISC_FILTERED = ".pdResult";
        public const string EXT_PILOT = ".group";
        public const string EXT_PILOT_XML = ".group.xml";
        public const string EXT_PRIDE_XML = ".pride.xml";
        public const string EXT_PERCOLATOR = ".perc.xml";
        public const string EXT_PERCOLATOR_XML = "results.xml";
        public const string EXT_MAX_QUANT = "msms.txt";
        public const string EXT_WATERS_MSE = "final_fragment.csv";
        public const string EXT_PROXL_XML = ".proxl.xml";
        public const string EXT_TSV = ".tsv";
        public const string EXT_MZTAB = ".mzTab";
        public const string EXT_MZTAB_TXT = "mztab.txt";
        public const string EXT_OPEN_SWATH = ".osw";
        public const string EXT_SPECLIB = ".speclib";
        // ReSharper restore LocalizableElement

        private ReadOnlyCollection<string> _inputFiles;

        public BiblioSpecLiteBuilder(string name, string outputPath, IList<string> inputFiles, IList<Target> targetSequences = null)
        {
            LibrarySpec = new BiblioSpecLiteSpec(name, outputPath);

            InputFiles = inputFiles;

            // FUTURE(bspratt) small molecule workflows
            if (targetSequences != null)
                TargetSequences = targetSequences.Where(t => t.IsProteomic).Select(t => t.Sequence).ToList();
        }

        public LibrarySpec LibrarySpec { get; private set; }
        public string OutputPath { get { return LibrarySpec.FilePath; } }

        public LibraryBuildAction Action { get; set; }
        public bool KeepRedundant { get; set; }
        public double? CutOffScore { get; set; }
        public string Id { get; set; }
        public bool IncludeAmbiguousMatches { get; set; }
        public IrtStandard IrtStandard { get; set; }
        public bool? PreferEmbeddedSpectra { get; set; }
        public bool DebugMode { get; set; }

        public IList<string> InputFiles
        {
            get { return _inputFiles; }
            private set { _inputFiles = value as ReadOnlyCollection<string> ?? new ReadOnlyCollection<string>(value); }
        }

        public IList<string> TargetSequences { get; private set; }

        public string AmbiguousMatchesMessage
        {
            get
            {
                return _ambiguousMatches != null && _ambiguousMatches.Any()
                    ? TextUtil.LineSeparate(
                        Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_,
                        string.Join(Environment.NewLine, _ambiguousMatches))
                    : string.Empty;
            }
        }

        public string BuildCommandArgs { get { return _buildCommandArgs; } }
        public string BuildOutput { get { return _buildOutput; } }

        private string[] _ambiguousMatches;
        private string _buildCommandArgs;
        private string _buildOutput;

        public bool BuildLibrary(IProgressMonitor progress)
        {
            _ambiguousMatches = null;
            IProgressStatus status = new ProgressStatus(Resources.BiblioSpecLiteBuilder_BuildLibrary_Preparing_to_build_library);
            progress.UpdateProgress(status);
            if (InputFiles.Any(f => f.EndsWith(EXT_PILOT)))
            {
                try
                {
                    InputFiles = VendorIssueHelper.ConvertPilotFiles(InputFiles, progress, status);
                    if (progress.IsCanceled)
                        return false;
                }
                catch (Exception x)
                {
                    progress.UpdateProgress(status.ChangeErrorException(x));
                    return false;
                }
            }

            string message = string.Format(Resources.BiblioSpecLiteBuilder_BuildLibrary_Building__0__library,
                                           Path.GetFileName(OutputPath));
            progress.UpdateProgress(status = status.ChangeMessage(message));
            string redundantLibrary = BiblioSpecLiteSpec.GetRedundantName(OutputPath);
            var blibBuilder = new BlibBuild(redundantLibrary, InputFiles, TargetSequences)
            {
                IncludeAmbiguousMatches = IncludeAmbiguousMatches,
                CutOffScore = CutOffScore,
                Id = Id,
                PreferEmbeddedSpectra = PreferEmbeddedSpectra,
                DebugMode = DebugMode,
            };

            bool retry = true;
            do
            {
                try
                {
                    if (!blibBuilder.BuildLibrary(Action, progress, ref status,
                        out _buildCommandArgs, out _buildOutput, out _ambiguousMatches))
                    {
                        return false;
                    }

                    retry = false;
                }
                catch (IOException x)
                {
                    if (IsLibraryMissingExternalSpectraError(x, out string spectrumFilename, out string resultsFilepath))
                    {
                        // replace the relative path to the results file (e.g. msms.txt) with the absolute path
                        string fullResultsFilepath = InputFiles.SingleOrDefault(o => o.EndsWith(resultsFilepath)) ??
                            throw new InvalidDataException(@"no results filepath from BiblioSpec error message", x);

                        // TODO: this will break if BiblioSpec output is translated to other languages
                        string messageWithFullFilepath =
                            x.Message.Replace(@"search results file '" + resultsFilepath,
                                              @"search results file '" + fullResultsFilepath);

                        var response =
                            progress.UpdateProgress(
                                status.ChangeErrorException(new IOException(messageWithFullFilepath, x)));
                        if (response == UpdateProgressResponse.cancel)
                            return false;
                        else if (response == UpdateProgressResponse.normal)
                            blibBuilder.PreferEmbeddedSpectra = true;
                        continue;
                    }

                    progress.UpdateProgress(status.ChangeErrorException(x));
                    return false;
                }
                catch (Exception x)
                {
                    Console.WriteLine(x.Message);
                    progress.UpdateProgress(status.ChangeErrorException(
                        new Exception(string.Format(Resources.BiblioSpecLiteBuilder_BuildLibrary_Failed_trying_to_build_the_redundant_library__0__,
                                                    redundantLibrary))));
                    return false;
                }
            } while (retry);

            var blibFilter = new BlibFilter();
            status = new ProgressStatus(message);
            progress.UpdateProgress(status);
            // Write the non-redundant library to a temporary file first
            try
            {
                using (var saver = new FileSaver(OutputPath))
                {
                    if (!blibFilter.Filter(redundantLibrary, saver.SafeName, progress, ref status))
                    {
                        return false;
                    }

                    saver.Commit();
                }
            }
            catch (IOException x)
            {
                progress.UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
            catch
            {
                progress.UpdateProgress(status.ChangeErrorException(
                    new Exception(string.Format(Resources.BiblioSpecLiteBuilder_BuildLibrary_Failed_trying_to_build_the_library__0__,
                                                OutputPath))));
                return false;
            }
            finally
            {
                if (!KeepRedundant)
                    FileEx.SafeDelete(redundantLibrary, true);
            }

            return true;
        }

        public static bool HasEmbeddedSpectra(string libraryInputFilepath)
        {
            return libraryInputFilepath.EndsWith(EXT_MAX_QUANT);
        }

        public static bool IsLibraryMissingExternalSpectraError(Exception errorException)
        {
            // ReSharper disable UnusedVariable
            return IsLibraryMissingExternalSpectraError(errorException, out string s1, out string s2);
            // ReSharper restore UnusedVariable
        }

        public static bool IsLibraryMissingExternalSpectraError(Exception errorException, out string spectrumFilename, out string resultsFilepath)
        {
            spectrumFilename = resultsFilepath = null;

            // TODO: this test (and the regex below) will break if BiblioSpec output is translated to other languages
            if (!errorException.Message.Contains(@"Could not find spectrum file"))
                return false;

            var messageParts = Regex.Match(errorException.Message, "Could not find spectrum file '([^[]+)\\[.*\\]' for search results file '([^']*)'");
            if (!messageParts.Success)
                throw new InvalidDataException(@"failed to parse filenames from BiblioSpec error message", errorException);

            spectrumFilename = messageParts.Groups[1].Value;
            resultsFilepath = messageParts.Groups[2].Value;

            return HasEmbeddedSpectra(resultsFilepath);
        }

        public static string BiblioSpecSupportedFileExtensions => @"mz5, mzML, raw, wiff, d, lcd, mzXML, cms2, ms2, or mgf";
    }
}
