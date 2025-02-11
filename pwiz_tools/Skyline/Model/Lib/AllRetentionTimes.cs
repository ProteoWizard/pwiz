using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class AllRetentionTimes
    {
        private readonly Dictionary<MsDataFileUri, int> _dataFileUriIndex = new Dictionary<MsDataFileUri, int>();
        private readonly Dictionary<string, int> _fileNameIndex = new Dictionary<string, int>();
        private readonly LibraryFiles _libraryFiles;
        private readonly LibKeyIndex _libKeyIndex;
        private readonly Dictionary<Target, ImmutableList<int>> _targetIndices = new Dictionary<Target, ImmutableList<int>>();

        private readonly Dictionary<Tuple<int, int>, RegressionLine> _regressionLines =
            new Dictionary<Tuple<int, int>, RegressionLine>();
        public AllRetentionTimes(FileTargetMatrix<ImmutableList<double>> matrix)
        {
            Matrix = matrix;
            _libKeyIndex = new LibKeyIndex(Matrix.Targets.Select(target => target.GetLibKey(Adduct.EMPTY).LibraryKey));
            _libraryFiles = new LibraryFiles(Matrix.FileNames.Select(source => source.Name));
        }

        public FileTargetMatrix<ImmutableList<double>> Matrix { get; private set; }

        private int IndexOfFileUri(MsDataFileUri msDataFileUri)
        {
            lock (_dataFileUriIndex)
            {
                if (_dataFileUriIndex.TryGetValue(msDataFileUri, out var index))
                {
                    return index;
                }

                index = _libraryFiles.FindIndexOf(msDataFileUri);
                _dataFileUriIndex[msDataFileUri] = index;
                return index;
            }
        }

        private int IndexOfFileName(string fileName)
        {
            lock (_fileNameIndex)
            {
                if (_fileNameIndex.TryGetValue(fileName, out int index))
                {
                    return index;
                }

                index = IndexOfFileUri(MsDataFileUri.Parse(fileName));
                _fileNameIndex[fileName] = index;
                return index;
            }
        }

        private ImmutableList<int> IndicesOfTarget(Target target)
        {
            lock (_targetIndices)
            {
                if (_targetIndices.TryGetValue(target, out var list))
                {
                    return list;
                }

                list = ImmutableList.ValueOf(_libKeyIndex.ItemsMatching(target.GetLibKey(Adduct.EMPTY), false)
                    .Select(item => item.OriginalIndex));
                _targetIndices[target] = list;
                return list;
            }
        }

        private RegressionLine GetRegression(int fileIndex1, int fileIndex2)
        {
            if (fileIndex1 < 0 || fileIndex2 < 0)
            {
                return null;
            }
            var key = Tuple.Create(fileIndex1, fileIndex2);
            lock (_regressionLines)
            {
                if (_regressionLines.TryGetValue(key, out var line))
                {
                    return line;
                }

                var start = DateTime.UtcNow;
                var xValues = new List<double>();
                var yValues = new List<double>();
                for (int iTarget = 0; iTarget < Matrix.Targets.Count; iTarget++)
                {
                    var xList = Matrix.Entries[iTarget][fileIndex1];
                    var yList = Matrix.Entries[iTarget][fileIndex2];
                    if (xList.Count == 0 || yList.Count == 0)
                    {
                        continue;
                    }
                    xValues.Add(xList.Min());
                    yValues.Add(yList.Min());
                }

                var regressionLine = new RegressionLine(xValues.ToArray(), yValues.ToArray());
                if (double.IsNaN(regressionLine.Slope) || double.IsNaN(regressionLine.Intercept))
                {
                    regressionLine = null;
                }
                _regressionLines[key] = regressionLine;
                var elapsed = DateTime.UtcNow - start;
                Console.Out.WriteLine(@"Aligned {0} to {1} with {2} points in {3}. Slope:{4} Intercept:{5}", Matrix.FileNames[fileIndex1].Name, Matrix.FileNames[fileIndex2].Name, xValues.Count, elapsed, regressionLine?.Slope, regressionLine?.Intercept);
                return regressionLine;
            }
        }

        public RegressionLine GetRegression(MsDataFileUri target, MsDataFileUri source)
        {
            return GetRegression(IndexOfFileUri(target), IndexOfFileUri(source));
        }

        public RegressionLine GetRegression(string target, string source)
        {
            return GetRegression(IndexOfFileName(target), IndexOfFileName(source));
        }

        public IList<double> GetRetentionTimes(Target target, MsDataFileUri fileUri)
        {
            return GetRetentionTimes(target, IndexOfFileUri(fileUri));
        }

        public IList<double> GetRetentionTimes(Target target, string filePath)
        {
            return GetRetentionTimes(target, IndexOfFileName(filePath));
        }

        private IList<double> GetRetentionTimes(Target target, int fileIndex)
        {
            if (fileIndex < 0)
            {
                return Array.Empty<double>();
            }
            IEnumerable<double> result = null;
            foreach (var targetIndex in IndicesOfTarget(target))
            {
                var times = Matrix.Entries[targetIndex][fileIndex];
                result = result?.Concat(times) ?? times;
            }

            if (result == null)
            {
                return ImmutableList.Empty<double>();
            }

            return result as IList<double> ?? result.ToList();
        }

        public IList<double> GetAlignedRetentionTimes(Target target, MsDataFileUri fileUri)
        {
            int fileIndex = IndexOfFileUri(fileUri);
            var result = new List<double>();
            var targetIndices = IndicesOfTarget(target);
            for (int iOtherFile = 0; iOtherFile < Matrix.FileNames.Count; iOtherFile++)
            {
                if (iOtherFile == fileIndex)
                {
                    continue;
                }

                var alignment = GetRegression(iOtherFile, fileIndex);
                if (alignment != null)
                {
                    foreach (var targetIndex in targetIndices)
                    {
                        result.AddRange(Matrix.Entries[targetIndex][iOtherFile].Select(alignment.GetY));
                    }
                }
            }

            return result;
        }

        public IList<double> GetBestRetentionTimes(SrmSettings settings, PeptideDocNode peptideDocNode,
            MsDataFileUri fileUri)
        {
            var targets = settings.GetLightAndHeavyTargets(peptideDocNode).ToList();
            var retentionTimes = targets.SelectMany(target => GetRetentionTimes(target, fileUri)).ToList();
            if (retentionTimes.Count > 0)
            {
                return retentionTimes;
            }

            return targets.SelectMany(target => GetAlignedRetentionTimes(target, fileUri)).ToList();
        }

        public IEnumerable<RetentionTimeSource> GetSourcesNotAlignedTo(MsDataFileUri target)
        {
            int targetIndex = IndexOfFileUri(target);
            if (targetIndex < 0)
            {
                return Matrix.FileNames;
            }

            return Enumerable.Range(0, Matrix.FileNames.Count)
                .Where(i => i != targetIndex && GetRegression(targetIndex, i) == null).Select(i => Matrix.FileNames[i]);
        }
    }
}
