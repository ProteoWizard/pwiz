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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ChromatogramManager : BackgroundLoader
    {
        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return previous == null ||
                !ReferenceEquals(document.Settings.MeasuredResults, previous.Settings.MeasuredResults);
        }

        protected override bool IsLoaded(SrmDocument document)
        {
            SrmSettings settings = document.Settings;
            return !settings.HasResults || settings.MeasuredResults.IsLoaded;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            if (document == null || !document.Settings.HasResults)
                return new IPooledStream[0];
            return document.Settings.MeasuredResults.ReadStreams;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            SrmSettings settings = container.Document.Settings;
            // If the document no longer contains any measured results, or the
            // measured results for the document are completely loaded.
            // TODO: Allow a single file loading to be canceled by removing it
            return !settings.HasResults || settings.MeasuredResults.IsLoaded;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var loader = new Loader(this, container, document, docCurrent);
            loader.Load();

            return false;
        }

        private sealed class Loader
        {
            private readonly ChromatogramManager _manager;
            private readonly IDocumentContainer _container;
            private readonly SrmDocument _document;
            private readonly SrmDocument _docCurrent;

            public Loader(ChromatogramManager manager, IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
            {
                _manager = manager;
                _container = container;
                _document = document;
                _docCurrent = docCurrent;
            }

            public void Load()
            {
                string documentFilePath = _container.DocumentFilePath;
                if (documentFilePath == null)
                    return;

                var results = _docCurrent.Settings.MeasuredResults;
                if (results.IsLoaded)
                    return;

                results.Load(_docCurrent, documentFilePath, new LoadMonitor(_manager, _container, results),
                            FinishLoad);
            }

            private void CancelLoad(MeasuredResults results)
            {
                if (results.StatusLoading != null)
                    _manager.UpdateProgress(results.StatusLoading.Cancel());
                _manager.EndProcessing(_document);                
            }

            private void FinishLoad(string documentPath, MeasuredResults resultsLoad)
            {
                if (resultsLoad == null)
                {
                    // Loading was cancelled
                    _manager.EndProcessing(_document);
                    return;
                }

                SrmDocument docNew, docCurrent;
                do
                {
                    docCurrent = _container.Document;
                    var results = docCurrent.Settings.MeasuredResults;
                    // If current document has no results, then cancel
                    if (results == null)
                    {
                        CancelLoad(resultsLoad);
                        return;
                    }
                    // Otherwise, switch to new cache
                    docNew = docCurrent.ChangeMeasuredResults(results.UpdateCaches(documentPath, resultsLoad));
                }
                while (!_manager.CompleteProcessing(_container, docNew, docCurrent));

                // Force a document changed event to keep progressive load going
                // until it is complete
                _manager.OnDocumentChanged(_container, new DocumentChangedEventArgs(docCurrent));
            }
        }
    }

    [XmlRoot("replicate")]
    [XmlRootAlias("chromatogram_group")]
    public sealed class ChromatogramSet : XmlNamedIdElement
    {
        /// <summary>
        /// Info for all files contained in this replicate
        /// </summary>
        private ReadOnlyCollection<ChromFileInfo> _msDataFileInfo;

        /// <summary>
        /// Ids used in XML to refer to the files in this replicate
        /// </summary>
        private string[] _fileLoadIds;

        public ChromatogramSet(string name, IEnumerable<string> msDataFileNames)
            : this(name, msDataFileNames, null)
        {            
        }

        public ChromatogramSet(string name, IEnumerable<string> msDataFileNames,
                OptimizableRegression optimizationFunction)
            : base(new ChromatogramSetId(), name)
        {
            MSDataFileInfos = msDataFileNames.ToList().ConvertAll(path => new ChromFileInfo(path));

            OptimizationFunction = optimizationFunction;
        }

        public IList<ChromFileInfo> MSDataFileInfos
        {
            get { return _msDataFileInfo; }
            set
            {
                // Make sure paths are unique
                _msDataFileInfo = MakeReadOnly(value.Distinct(ChromFileInfo.PATH_COMPARER).ToArray());
            }
        }

        public int FileCount { get { return _msDataFileInfo.Count; } }

        public IEnumerable<string> MSDataFilePaths { get { return MSDataFileInfos.Select(info => info.FilePath); } }

        public bool IsLoaded { get { return !MSDataFileInfos.Contains(info => !info.FileWriteTime.HasValue); } }

        public OptimizableRegression OptimizationFunction { get; private set; }

        public int IndexOfFile(ChromatogramGroupInfo chromGroupInfo)
        {
            return MSDataFileInfos.IndexOf(info => Equals(info.FilePath, chromGroupInfo.FilePath));
        }

        public int IndexOfId(string id)
        {
            return Array.IndexOf(_fileLoadIds, id);
        }

        #region Property change methods

        public ChromatogramSet ChangeMSDataFilePaths(IList<string> prop)
        {
            var set = ImClone(this);

            set.MSDataFileInfos = prop.ToList().ConvertAll(path => new ChromFileInfo(path));
            set.CalcCachedFlags(MSDataFileInfos.ToDictionary(info => info.FilePath), null, null);
            
            return set;
        }

        public ChromatogramSet ChangeFileCacheFlags(IDictionary<string, ChromFileInfo> cachedPaths,
            HashSet<string> cachedFileNames, string cachePath)
        {
            var set = ImClone(this);

            set.CalcCachedFlags(cachedPaths, cachedFileNames, cachePath);

            if (ReferenceEquals(MSDataFileInfos, set.MSDataFileInfos))
                return this;

            return set;
        }

        /// <summary>
        /// Calculates the fileCacheFlags from a collection of cached paths.
        /// This function modifies 'this', and should only be called on a clone
        /// before it is returned from a change operation.
        /// <para>
        /// If a path is not found in the cached set, its name is checked in
        /// the names set, and if found, then disk is checked to see if the
        /// file location has been moved.</para>
        /// </summary>
        /// <param name="cachedPaths">Set of known cached paths</param>
        /// <param name="cachedFileNames">Set of known cached file names</param>
        /// <param name="cachePath">Final cache path</param>
        private void CalcCachedFlags(IDictionary<string, ChromFileInfo> cachedPaths,
            ICollection<string> cachedFileNames, string cachePath)
        {
            ChromFileInfo[] fileInfos = new ChromFileInfo[FileCount];
            for (int i = 0; i < fileInfos.Length; i++)
            {
                fileInfos[i] = MSDataFileInfos[i];
                
                string path = fileInfos[i].FilePath;

                ChromFileInfo fileInfo;
                if (cachedPaths.TryGetValue(path, out fileInfo))
                    fileInfos[i] = fileInfo;
                else if (cachedFileNames == null || cachedFileNames.Contains(SampleHelp.GetFileName(path)))
                {
                    // If the name but not the file was found, check for an
                    // existing file in the cache file's directory.
                    string dataFilePathPart;
                    string dataFilePath = GetExistingDataFilePath(cachePath, path, out dataFilePathPart);
                    if (cachedPaths.TryGetValue(dataFilePath, out fileInfo))
                        fileInfos[i] = fileInfo;
                }
            }

            if (!ArrayUtil.EqualsDeep(MSDataFileInfos, fileInfos))
                MSDataFileInfos = fileInfos;
        }

        public ChromatogramSet ChangeOptimizationFunction(OptimizableRegression prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.OptimizationFunction = v, prop);
        }        

        #endregion

        public static string GetExistingDataFilePath(string cachePath, string dataFilePath, out string dataFilePathPart)
        {
            dataFilePathPart = SampleHelp.GetPathFilePart(dataFilePath);
            // Check file (directory for Waters) existence, because ProteoWizard can hang on this
            if (!File.Exists(dataFilePathPart) && !Directory.Exists(dataFilePathPart))
            {
                // Check the most common case where the file is in the same directory
                // where the cache is being written.
                // CONSIDER: Store relative paths instead?
                string localPath = Path.Combine(Path.GetDirectoryName(cachePath) ?? "",
                                                Path.GetFileName(dataFilePathPart) ?? "");  // Resharper
                if (!File.Exists(localPath) && !Directory.Exists(localPath))
                    return null;

                // Update variables to point to the new location.
                if (Equals(dataFilePath, dataFilePathPart))
                    dataFilePath = localPath;
                else
                    dataFilePath = localPath + dataFilePath.Substring(dataFilePathPart.Length);
                dataFilePathPart = localPath;
            }
            return dataFilePath;
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private ChromatogramSet()
            : base(new ChromatogramSetId())
        {
        }

        public static ChromatogramSet Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromatogramSet());
        }

        private enum EL
        {
            sample_file,
            replicate_file,
            chromatogram_file
        }

        private enum ATTR
        {
            id,
            file_path
        }

        private static readonly IXmlElementHelper<OptimizableRegression>[] OPTIMIZATION_HELPERS =
        {
            new XmlElementHelperSuper<CollisionEnergyRegression, OptimizableRegression>(),                 
            new XmlElementHelperSuper<DeclusteringPotentialRegression, OptimizableRegression>(),                 
        };

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);

            // Consume tag
            reader.Read();

            // Check if there is an optimization function element, and read
            // if if there is.
            IXmlElementHelper<OptimizableRegression> helper =
                reader.FindHelper(OPTIMIZATION_HELPERS);
            if (helper != null)
                OptimizationFunction = helper.Deserialize(reader);

            var msDataFilePaths = new List<string>();
            var fileLoadIds = new List<string>();
            while (reader.IsStartElement(EL.sample_file) ||
                    reader.IsStartElement(EL.replicate_file) ||
                    reader.IsStartElement(EL.chromatogram_file))
            {
                msDataFilePaths.Add(reader.GetAttribute(ATTR.file_path));
                string id = reader.GetAttribute(ATTR.id) ?? GetFileSaveId(fileLoadIds.Count);
                fileLoadIds.Add(id);
                reader.Read();
            }

            MSDataFileInfos = msDataFilePaths.ConvertAll(path => new ChromFileInfo(path));
            _fileLoadIds = fileLoadIds.ToArray();

            // Consume end tag
            reader.ReadEndElement();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);

            // Write optimization element, if present
            if (OptimizationFunction != null)
            {
                IXmlElementHelper<OptimizableRegression> helper = XmlUtil.FindHelper(
                    OptimizationFunction, OPTIMIZATION_HELPERS);
                if (helper == null)
                    throw new InvalidOperationException("Attempt to serialize list containing invalid type.");
                writer.WriteElement(helper.ElementNames[0], OptimizationFunction);                
            }

            int i = 0, countFiles = FileCount;
            foreach (var path in MSDataFilePaths)
            {
                writer.WriteStartElement(EL.sample_file);
                if (countFiles > 1)
                    writer.WriteAttribute(ATTR.id, GetFileSaveId(i++));
                writer.WriteAttribute(ATTR.file_path, path);
                writer.WriteEndElement();
            }
        }

        public string GetFileSaveId(int index)
        {
            return string.Format("{0}_f{1}", Helpers.MakeXmlId(Name), index);
        }

        #endregion

        #region object overrides

        public bool Equals(ChromatogramSet obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && ArrayUtil.EqualsDeep(obj.MSDataFileInfos, MSDataFileInfos);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as ChromatogramSet);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode()*397) ^ MSDataFileInfos.GetHashCodeDeep();
            }
        }

        #endregion
    }

    /// <summary>
    /// Identity class to allow identity equality on <see cref="ChromatogramSetId"/>.
    /// </summary>
    public sealed class ChromatogramSetId : Identity
    {        
    }

    public sealed class ChromFileInfo : Immutable
    {
        public ChromFileInfo(string filePath)
            : this(filePath, null, null)
        {            
        }

        public ChromFileInfo(ChromCachedFile cachedFile)
            : this(cachedFile.FilePath, cachedFile.FileWriteTime, cachedFile.RunStartTime)
        {            
        }

        private ChromFileInfo(string filePath, DateTime? fileWriteTime, DateTime? runStartTime)
        {
            Id = new ChromFileInfoId();
            FilePath = filePath;
            FileWriteTime = fileWriteTime;
            RunStartTime = runStartTime;
        }

        public Identity Id { get; private set; }
        public string FilePath { get; private set; }
        public DateTime? FileWriteTime { get; private set; }
        public DateTime? RunStartTime { get; private set; }

        #region object overrides

        public bool Equals(ChromFileInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Id, Id) &&
                Equals(other.FilePath, FilePath) &&
                other.FileWriteTime.Equals(FileWriteTime) &&
                other.RunStartTime.Equals(RunStartTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ChromFileInfo)) return false;
            return Equals((ChromFileInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Id.GetHashCode();
                result = (result*397) ^ FilePath.GetHashCode();
                result = (result*397) ^ (FileWriteTime.HasValue ? FileWriteTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (RunStartTime.HasValue ? RunStartTime.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion

        #region FilePath comparer
        
        public static readonly PathComparer PATH_COMPARER = new PathComparer();

        public class PathComparer : IEqualityComparer<ChromFileInfo>
        {
            public bool Equals(ChromFileInfo f1, ChromFileInfo f2)
            {
                return Equals(f1.FilePath, f2.FilePath);
            }

            public int GetHashCode(ChromFileInfo f)
            {
                return f.GetHashCode();
            }
        }

        #endregion
    }

    public sealed class ChromFileInfoId : Identity
    {        
    }

    /// <summary>
    /// Helper functions for specifying a single sample injected into a mass
    /// spectrometer.
    /// 
    /// Ideally this would be represented with a complete object, but that would
    /// require both XML and cache format changes.  Implemented late in v0.5,
    /// the simplest solution is to encode the necessary information into the
    /// existing path string used to identify a single sample file.
    /// </summary>
    public static class SampleHelp
    {
        public static string EncodePath(string filePath, string sampleName, int sampleIndex)
        {
            return string.Format("{0}|{1}|{2}", filePath, sampleName, sampleIndex);
        }

        public static string EscapeSampleId(string sampleId)
        {
            var invalidFileChars = Path.GetInvalidFileNameChars();
            var invalidNameChars = new[] {',', '.', ';'};
            if (sampleId.IndexOfAny(invalidFileChars) == -1 &&
                    sampleId.IndexOfAny(invalidNameChars) == -1)
                return sampleId;
            var sb = new StringBuilder();
            foreach (char c in sampleId)
                sb.Append(invalidFileChars.Contains(c) || invalidNameChars.Contains(c) ? '_' : c);
            return sb.ToString();
        }

        public static string GetPathFilePart(string path)
        {
            if (path.IndexOf('|') == -1)
                return path;
            return path.Split('|')[0];
        }

        public static bool HasSamplePart(string path)
        {
            string[] parts = path.Split('|');

            int sampleIndex;
            return parts.Length == 3 && int.TryParse(parts[2], out sampleIndex);
        }

        public static string GetPathSampleNamePart(string path)
        {
            if (path.IndexOf('|') == -1)
                return null;
            return path.Split('|')[1];
        }

        public static int GetPathSampleIndexPart(string path)
        {
            int sampleIndex = -1;
            if (path.IndexOf('|') != -1)
            {
                string[] parts = path.Split('|');
                int index;
                if (parts.Length == 3 && int.TryParse(parts[2], out index))
                    sampleIndex = index;
            }
            return sampleIndex;
        }        

        /// <summary>
        /// Returns just the file name from a path that may contain sample information.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The file name part</returns>
        public static string GetFileName(string path)
        {
            return Path.GetFileName(GetPathFilePart(path));
        }

        /// <summary>
        /// Returns a sample name for any file path, using either the available sample
        /// information on the path, or the file basename, if no sample information is present.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The sample name part or file basename</returns>
        public static string GetFileSampleName(string path)
        {
            return GetPathSampleNamePart(path) ?? Path.GetFileNameWithoutExtension(path);
        }
    }
}
