/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Threading.Tasks;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// Holds all of the retention times for all peptides loaded from multiple spectral libraries.
    /// </summary>
    public class LoadedRetentionTimes
    {
        private readonly IDictionary<LibraryRetentionTimes, RetentionTimesAlignedToFile> _retentionTimesAlignedToFilesDict = new Dictionary<LibraryRetentionTimes, RetentionTimesAlignedToFile>();
        public const double REFINEMENT_THRESHHOLD = 0.99;
        /// <summary>
        /// Reads the retention times from all of the libraries used by an SrmDocument.
        /// </summary>
        public static Task<LoadedRetentionTimes> StartLoadFromAllLibraries(SrmDocument document)
        {
            var libraries = document.Settings.PeptideSettings.Libraries.Libraries;
            var tasks =
                libraries.Select(
                    library =>
                    library.StartGetAllRetentionTimes().ContinueWith(
                        task =>
                        new KeyValuePair<Library, IDictionary<string, LibraryRetentionTimes>>(library, task.Result))).
                    ToArray();
            if (tasks.Length == 0)
            {
                var taskCompletionSource = new TaskCompletionSource<LoadedRetentionTimes>();
                taskCompletionSource.SetResult(new LoadedRetentionTimes(new KeyValuePair<Library, IDictionary<string, LibraryRetentionTimes>>[0]));
                return taskCompletionSource.Task;
            }
            return Task.Factory.ContinueWhenAll(tasks, completedTasks => new LoadedRetentionTimes(completedTasks.Select(task => task.Result).ToArray()));
        }

        
        /// <summary>
        /// Constructs a LoadedRetentionTimes from a dictionary of data file path (i.e. whatever the Library
        /// says the data file path is, which for BiblioSpecLiteLibraries has the path and extension stripped off)
        /// to LibraryRetentionTimes.
        /// </summary>
        public LoadedRetentionTimes(IEnumerable<KeyValuePair<Library, IDictionary<string, LibraryRetentionTimes>>> libraryRetentionTimesByPath)
        {
            LibraryRetentionTimesByPath = Array.AsReadOnly(libraryRetentionTimesByPath.ToArray());
        }

        public IList<KeyValuePair<Library, IDictionary<string, LibraryRetentionTimes>>> LibraryRetentionTimesByPath { get; private set; }
        public bool IsValidFor(SrmDocument document)
        {
            var librarySet = new HashSet<Library>(document.Settings.PeptideSettings.Libraries.Libraries.Where(library=>library != null && library.IsLoaded)).ToArray();
            return librarySet.SequenceEqual(LibraryRetentionTimesByPath.Select(entry=>entry.Key));
        }

        /// <summary>
        /// Creates a <see cref="RetentionTimesAlignedToFile"/> where the retention times for all
        /// of the other data files are aligned to the specified file.
        /// Returns null if the file to be aligned against was not found in any library.
        /// </summary>
        public RetentionTimesAlignedToFile GetRetentionTimesAlignedToFile(string filePath)
        {
            LibraryRetentionTimes targetTimes = FindRetentionTimes(filePath);
            if (targetTimes == null)
            {
                return null;
            }
            return GetRetentionTimesAlignedToFile(targetTimes);
        }

        public RetentionTimesAlignedToFile GetRetentionTimesAlignedToFile(LibraryRetentionTimes targetTimes)
        {
            RetentionTimesAlignedToFile result;
            lock (_retentionTimesAlignedToFilesDict)
            {
                if (_retentionTimesAlignedToFilesDict.TryGetValue(targetTimes, out result))
                {
                    return result;
                }
            }
            result = new RetentionTimesAlignedToFile(targetTimes);
            foreach (var libraryEntry in LibraryRetentionTimesByPath)
            {
                foreach (var entry in libraryEntry.Value)
                {
                    if (ReferenceEquals(targetTimes, entry.Value))
                    {
                        continue;
                    }
                    result.AddFile(libraryEntry.Key, entry.Value);
                }
            }
            lock (_retentionTimesAlignedToFilesDict)
            {
                _retentionTimesAlignedToFilesDict[targetTimes] = result;
            }
            return result;
        }


        public LibraryRetentionTimes FindRetentionTimes(string filePath)
        {
            return FindRetentionTimes(filePath, false);
        }
        /// <summary>
        /// Returns the <see cref="LibraryRetentionTimes"/> that matches the specified filename,
        /// or null if none could be found.
        /// </summary>
        public LibraryRetentionTimes FindRetentionTimes(string filePath, bool exact)
        {
            LibraryRetentionTimes libraryRetentionTimes;
            if (!exact)
            {
                libraryRetentionTimes = FindRetentionTimes(filePath, true);
                if (libraryRetentionTimes != null)
                {
                    return libraryRetentionTimes;
                }
            }
            foreach (var entry in LibraryRetentionTimesByPath)
            {
                if (exact)
                {
                    if (entry.Value.TryGetValue(filePath, out libraryRetentionTimes))
                    {
                        return libraryRetentionTimes;
                    }
                }
                else
                {
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    foreach (var libEntry in entry.Value)
                    {
                        var libraryBaseName = Path.GetFileNameWithoutExtension(libEntry.Key);
                        if (MeasuredResults.IsBaseNameMatch(baseName, libraryBaseName))
                        {
                            return libEntry.Value;
                        }
                    }
                }
            }
            return null;
        }

//    public class RetentionTimeLoader : IDisposable
//    {
//        private IDocumentUIContainer _documentUIContainer;
//        private Task<LoadedRetentionTimes> _loadRetentionTimesTask;
//        public RetentionTimeLoader(IDocumentUIContainer documentUIContainer)
//        {
//            DocumentUIContainer = documentUIContainer;
//        }
//
//        public IDocumentUIContainer DocumentUIContainer
//        {
//            get { return _documentUIContainer; }
//            set
//            {
//                if (ReferenceEquals(DocumentUIContainer, value))
//                {
//                    return;
//                }
//                if (DocumentUIContainer != null)
//                {
//                    DocumentUIContainer.UnlistenUI(OnDocumentUIChanged);
//                }
//                _documentUIContainer = value;
//                if (DocumentUIContainer != null)
//                {
//                    DocumentUIContainer.ListenUI(OnDocumentUIChanged);
//                }
//            }
//        }
//
//        private void OnDocumentUIChanged(object sender, DocumentChangedEventArgs documentChangedEventArgs)
//        {
//            
//        }
//
//        public Task<LoadedRetentionTimes> StartLoadRetentionTimes()
//        {
//            if (_loadRetentionTimesTask != null)
//            {
//                return _loadRetentionTimesTask;
//            }
//            _loadRetentionTimesTask = Task.Factory.StartNew()
//        }
    }

    /// <summary>
    /// Contains the results of aligning a set of MS2 Id's from one file to
    /// another.
    /// </summary>
    public class AlignedFile
    {
        private AlignedFile(Library originalLibrary, LibraryRetentionTimes originalTimes)
        {
            OriginalLibrary = originalLibrary;
            OriginalTimes = originalTimes;
        }

        public Library OriginalLibrary { get; private set; }

        /// <summary>
        /// The original times that were read out of the spectral library.
        /// </summary>
        public LibraryRetentionTimes OriginalTimes { get; private set; }

        public RetentionTimeRegression Regression { get; private set; }
        public RetentionTimeRegression RegressionRefined { get;private set; }
        public HashSet<int> OutlierIndexes { get; private set; }
        /// <summary>
        /// The slope and intercept for the alignment.
        /// </summary>
        public RegressionLineElement RegressionLine { get { return (RegressionRefined ?? Regression).Conversion; } }

        /// <summary>
        /// The number of points that were used to do the alignment.  (i.e. the number of peptide sequences which were in
        /// common between the two data files)
        /// </summary>
        public int RegressionPointCount { get { return Regression.PeptideTimes.Count; } }

        public double[] GetRetentionTimes(string peptideSequence)
        {
            return OriginalTimes.GetRetentionTimes(peptideSequence).Select(time => RegressionLine.GetY(time)).ToArray();
        }

        public IList<double> YValues { get { return Array.AsReadOnly(Regression.PeptideTimes.Select(measuredRetentionTime => measuredRetentionTime.RetentionTime).ToArray()); } }
        public IList<double> XValues { get
        {
            return
                Array.AsReadOnly(
                    Regression.PeptideTimes.Select(
                        measuredRetentionTime =>
                        Regression.Calculator.ScoreSequence(measuredRetentionTime.PeptideSequence)).Cast<double>().ToArray());
        } }

        /// <summary>
        /// Align retention times with a target.
        /// For the MS2 Id's that are found in both the target and the timesToAlign, the MS2 id's 
        /// are plotted against each other, and a linear regression is performed.
        /// In cases where there is more than one MS2 id in either file, only the earliest MS2 id from
        /// each file is used.
        /// </summary>
        public static AlignedFile AlignLibraryRetentionTimes(RetentionTimesAlignedToFile target,
                                                             Library library,
                                                             LibraryRetentionTimes timesToAlign)
        {
            var calculator = new RetentionTimeProviderScoreCalculator(timesToAlign.Name, timesToAlign);
            var alignFromTimes = timesToAlign.PeptideRetentionTimes.ToLookup(
                measuredRetentionTime => measuredRetentionTime.PeptideSequence,
                measuredRetentionTime => measuredRetentionTime.RetentionTime);
            var xValues = new List<double>();
            var yValues = new List<double>();
            var targetTimes = new List<MeasuredRetentionTime>();
            foreach (var grouping in alignFromTimes)
            {
                var toTimes = target.TargetTimes.GetRetentionTimes(grouping.Key);
                if (toTimes.Length == 0)
                {
                    continue;
                }
                xValues.Add(grouping.Min());
                double targetTime = toTimes.Min();
                yValues.Add(targetTime);
                targetTimes.Add(new MeasuredRetentionTime(grouping.Key, targetTime));
            }
            RetentionTimeStatistics stats;
            var regression = RetentionTimeRegression.CalcRegression(timesToAlign.Name + ":" + target.Name,
                                                                    new[] {calculator}, targetTimes, out stats);
            RetentionTimeRegression regressionRefined = null;
            HashSet<int> outIndexes = new HashSet<int>();
            if (stats.R < LoadedRetentionTimes.REFINEMENT_THRESHHOLD)
            {
                var cache = new RetentionTimeScoreCache(new[] {calculator}, new MeasuredRetentionTime[0], null);
                RetentionTimeStatistics statsRefined = stats;
                regressionRefined = regression.FindThreshold(LoadedRetentionTimes.REFINEMENT_THRESHHOLD, 2, 0,
                                                             targetTimes.Count, new MeasuredRetentionTime[0], targetTimes, stats,
                                                             calculator, cache, () => false, ref statsRefined,
                                                             ref outIndexes);

            }
                
                
//               RetentionTimeRegression.FindThreshold(LoadedRetentionTimes.REFINEMENT_THRESHHOLD, 2,
//                                                               targetTimes, targetTimes, targetTimes, calculator,
//                                                               () => false);
            
            return new AlignedFile(library, timesToAlign)
                       {
                            Regression = regression,
                            RegressionRefined = regressionRefined,
                            OutlierIndexes = outIndexes,
                       };
        }

    }

    /// <summary>
    /// Holds a set of alignments which are all aligned against the same file.
    /// </summary>
    public class RetentionTimesAlignedToFile
    {
        private readonly List<AlignedFile> _files = new List<AlignedFile>();

        public RetentionTimesAlignedToFile(LibraryRetentionTimes targetTimes)
        {
            TargetTimes = targetTimes;
            MaxResidual = double.PositiveInfinity;
        }

        public LibraryRetentionTimes TargetTimes { get; private set; }

        public string Name
        {
            get { return TargetTimes.Name; }
        }

        public IEnumerable<AlignedFile> AlignedFiles
        {
            get { return _files.AsReadOnly(); }
        }

        public double MaxResidual { get; set; }

        public void AddFile(Library library, LibraryRetentionTimes libraryRetentionTimes)
        {
            _files.Add(AlignedFile.AlignLibraryRetentionTimes(this, library, libraryRetentionTimes));
        }

        public double[] GetAlignedRetentionTimes(string modifiedSequence)
        {
            return AlignedFiles
                .SelectMany(alignedLibrary => alignedLibrary.GetRetentionTimes(modifiedSequence))
                .ToArray();
        }
    }
    internal class RetentionTimeProviderScoreCalculator : RetentionScoreCalculatorSpec
    {
        public RetentionTimeProviderScoreCalculator(string name, IRetentionTimeProvider retentionTimeProvider)
            : base(name)
        {
            RetentionTimeProvider = retentionTimeProvider;
        }

        public IRetentionTimeProvider RetentionTimeProvider { get; private set; }
        public override double? ScoreSequence(string modifiedSequence)
        {
            return RetentionTimeProvider.GetRetentionTime(modifiedSequence);
        }

        public override double UnknownScore
        {
            get { return double.NaN; }
        }

        public override IEnumerable<string> ChooseRegressionPeptides(IEnumerable<string> peptides)
        {
            return peptides.Where(peptide => null != ScoreSequence(peptide));
        }

        public override IEnumerable<string> GetStandardPeptides(IEnumerable<string> peptides)
        {
            return ChooseRegressionPeptides(peptides);
        }
    }
}


