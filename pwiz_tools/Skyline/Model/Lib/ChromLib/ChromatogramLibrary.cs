/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NHibernate;
using NHibernate.Criterion;
using NHibernate.Exceptions;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.ChromLib.Data;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.ChromLib
{
    [XmlRoot("chromatogram_library")]
    public class ChromatogramLibrary : CachedLibrary<ChromLibSpectrumInfo>
    {
        private readonly PooledSessionFactory _pooledSessionFactory;
        public const string EXT_CACHE = ".clc";

        private ChromatogramLibrarySourceInfo[] _librarySourceFiles;
        private LibraryFiles _libraryFiles = LibraryFiles.EMPTY;
        private ChromatogramLibraryIrt[] _libraryIrts;

        public ChromatogramLibrary(ChromatogramLibrarySpec chromatogramLibrarySpec) : base(chromatogramLibrarySpec)
        {
            LibrarySpec = chromatogramLibrarySpec;
            FilePath = LibrarySpec.FilePath;
            CachePath = Path.Combine(PathEx.GetDirectoryName(FilePath) ?? string.Empty,
                                     Path.GetFileNameWithoutExtension(FilePath) + EXT_CACHE);
            _libraryIrts = new ChromatogramLibraryIrt[0];
            _librarySourceFiles = new ChromatogramLibrarySourceInfo[0];
        }

        public ChromatogramLibrary(ChromatogramLibrarySpec chromatogramLibrarySpec, IStreamManager streamManager)
            : this(chromatogramLibrarySpec)
        {
            _pooledSessionFactory = new PooledSessionFactory(streamManager.ConnectionPool, typeof (ChromLibEntity),
                                                             chromatogramLibrarySpec.FilePath);
        }

        public ChromatogramLibrarySpec LibrarySpec { get; private set; }
        public string FilePath { get; private set; }

        public string PanoramaServer { get; private set; }
        public string SchemaVersion { get; private set; }
        public int LibraryRevision { get; private set; }

        protected override SpectrumHeaderInfo CreateSpectrumHeaderInfo(ChromLibSpectrumInfo info)
        {
            return new ChromLibSpectrumHeaderInfo(Name, info.PeakArea, info.Protein);
        }

        protected override SpectrumPeaksInfo.MI[] ReadSpectrum(ChromLibSpectrumInfo info)
        {
            return info.TransitionAreas.ToArray();
        }

        protected override LibraryChromGroup ReadChromatogram(ChromLibSpectrumInfo info)
        {
            using (var session = _pooledSessionFactory.Connection.OpenSession())
            {
                var precursor = GetPrecursor(session, info.Id);
                if (null != precursor)
                {
                    var timeIntensities = precursor.ChromatogramData;
                    double height = 0;
                    var chromDatas = new List<LibraryChromGroup.ChromData>();
                    var precursor13 = precursor as Precursor.Format1Dot3;
                    foreach (var transition in precursor.Transitions)
                    {
                        var transition13 = transition as Data.Transition.Format1Dot3;
                        var ionType = Helpers.ParseEnum(transition.FragmentType, IonType.y);
                        IonMobilityValue ionMobility = null;
                        var ionMobilityAndCCS = precursor13?.GetIonMobilityAndCCS() ?? IonMobilityAndCCS.EMPTY;
                        if (ionMobilityAndCCS.IonMobility.Units != eIonMobilityUnits.none)
                        {
                            var im = (ionType == IonType.precursor)
                                ? ionMobilityAndCCS.IonMobility.Mobility
                                : ionMobilityAndCCS.GetHighEnergyIonMobility();
                            if (im.HasValue)
                            {
                                ionMobility = IonMobilityValue.GetIonMobilityValue(im, ionMobilityAndCCS.IonMobility.Units);
                            }
                        }

                        var adduct = transition.GetAdduct();
                        if (adduct.AdductCharge == 0)
                        {
                            adduct = precursor.GetAdduct();
                        }

                        chromDatas.Add(new LibraryChromGroup.ChromData
                            {
                                Mz = transition.Mz,
                                Height = transition.Height,
                                Intensities = timeIntensities?.Intensities[transition.ChromatogramIndex] ?? new float[0],
                                Charge = adduct,
                                IonType = ionType,
                                Ordinal = transition.FragmentOrdinal,
                                MassIndex = transition.MassIndex,
                                FragmentName = transition13?.FragmentName,
                                IonMobility = ionMobility
                        });
                        height = Math.Max(height, transition.Height);
                    }
                    var precursorRetentionTime =
                        session.CreateCriteria<PrecursorRetentionTime>()
                               .Add(Restrictions.Eq(@"SampleFile", precursor.SampleFile))
                               .Add(Restrictions.Eq(@"Precursor", precursor))
                               .List<PrecursorRetentionTime>()
                               .FirstOrDefault();
                    double startTime = 0;
                    double endTime = 0;
                    double retentionTime = 0;
                    if (precursorRetentionTime != null)
                    {
                        startTime = precursorRetentionTime.StartTime;
                        endTime = precursorRetentionTime.EndTime;
                        retentionTime = precursorRetentionTime.RetentionTime;
                    }

                    var ccs = precursor13?.CCS ?? 0;
                    return new LibraryChromGroup
                        {
                            RetentionTime = retentionTime,
                            CCS = (ccs == 0) ? (double?)null : ccs,
                            StartTime = startTime,
                            EndTime = endTime,
                            Times = timeIntensities?.Times ?? new float[0],
                            ChromDatas = chromDatas
                        };
                }
            }
            return null;
        }

        protected Data.Peptide GetPeptide(ISession session, int id)
        {
            try
            {
                return session.Get<Data.Peptide.Format1Dot3>(id);
            }
            catch (HibernateException)
            {
                return session.Get<Data.Peptide>(id);
            }
        }

        protected Data.Transition GetTransition(ISession session, int id)
        {
            try
            {
                return session.Get<Data.Transition.Format1Dot3>(id);
            }
            catch (HibernateException)
            {
                return session.Get<Data.Transition>(id);
            }
        }


        protected Precursor GetPrecursor(ISession session, int id)
        {
            try
            {
                return session.Get<Precursor.Format1Dot3>(id);
            }
            catch (HibernateException)
            {
                try
                {
                    return session.Get<Precursor.Format1Dot2>(id);
                }
                catch (HibernateException)
                {
                    return session.Get<Precursor>(id);
                }
            }
        }

        protected override LibrarySpec CreateSpec()
        {
            return new ChromatogramLibrarySpec(Name, FilePath);
        }

        public override string SpecFilter
        {
            get { return TextUtil.FileDialogFiltersAll(ChromatogramLibrarySpec.FILTER_CLIB); }
        }

        public override IPooledStream ReadStream
        {
            get { return _pooledSessionFactory; }
        }

        public override bool IsSameLibrary(Library library)
        {
            var chromatogramLibrary = library as ChromatogramLibrary;
            return null != chromatogramLibrary && Equals(PanoramaServer, chromatogramLibrary.PanoramaServer);
        }

        public override int CompareRevisions(Library library)
        {
            return LibraryRevision.CompareTo(((ChromatogramLibrary) library).LibraryRevision);
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                return new LibraryDetails
                {
                    Format = @"ChromatogramLibrary",
                    Revision = LibraryRevision.ToString(LocalizationHelper.CurrentCulture),
                    SpectrumCount = 0,
                    DataFiles = LibraryFiles.FilePaths.Select(f => new SpectrumSourceFileDetails(f)).ToList()
                };
            }
        }

        public override LibraryFiles LibraryFiles
        {
            get { return _libraryFiles;}
        }

        public override int? FileCount
        {
            get { return _librarySourceFiles.Length; }
        }

        private int FindSource(MsDataFileUri filePath)
        {
            return _libraryFiles.FindIndexOf(filePath);
        }
        
        public override bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                var ionMobility = _libraryEntries[i].IonMobility;
                if (IonMobilityAndCCS.IsNullOrEmpty(ionMobility))
                {
                    ionMobilities = null;
                    return false;
                }
                ionMobilities = new[] {ionMobility};
                return true;
            }

            return base.TryGetIonMobilityInfos(key, filePath, out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            return TryGetIonMobilityInfos(targetIons, FindSource(filePath), out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            if (fileIndex >= 0 && fileIndex < _librarySourceFiles.Length)
            {
                ILookup<LibKey, IonMobilityAndCCS> ionMobilitiesLookup;
                var source = _librarySourceFiles[fileIndex];
                if (targetIons != null)
                {
                    if (!targetIons.Any())
                    {
                        ionMobilities = null;
                        return true; // return value false means "that's not a proper file index"'
                    }

                    ionMobilitiesLookup = targetIons.SelectMany(target => _libraryEntries.ItemsMatching(target, true)).
                        Where(entry => entry.SampleFileId == fileIndex).ToLookup(
                        entry => entry.Key,
                        entry => entry.IonMobility);
                }
                else
                {
                    ionMobilitiesLookup = _libraryEntries.ToLookup(
                        entry => entry.Key,
                        entry => entry.IonMobility);
                }
                var ionMobilitiesDict = ionMobilitiesLookup.ToDictionary(
                    grouping => grouping.Key,
                    grouping => 
                    {
                        var array = grouping.Where(v => !IonMobilityAndCCS.IsNullOrEmpty(v)).ToArray();
                        Array.Sort(array);
                        return array;
                    });

                var nonEmptyIonMobilitiesDict = ionMobilitiesDict
                    .Where(kvp => kvp.Value.Length > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                ionMobilities = nonEmptyIonMobilitiesDict.Any() ? new LibraryIonMobilityInfo(source.FilePath, false, nonEmptyIonMobilitiesDict) : null;
                return true;  // return value false means "that's not a proper file index"'
            }

            return base.TryGetIonMobilityInfos(targetIons, fileIndex, out ionMobilities);
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, out LibraryIonMobilityInfo ionMobilities)
        {
            if (targetIons != null && targetIons.Length > 0)
            {
                var ionMobilitiesDict = new Dictionary<LibKey, IonMobilityAndCCS[]>();
                foreach (var target in targetIons)
                {
                    foreach (var matchedItem in _libraryEntries.ItemsMatching(target, true))
                    {
                        var matchedTarget = matchedItem.Key;
                        var match = matchedItem.IonMobility;
                        if (IonMobilityAndCCS.IsNullOrEmpty(match))
                            continue;
                        if (ionMobilitiesDict.TryGetValue(matchedTarget, out var mobilities))
                        {
                            var newMobilities = mobilities.ToList();
                            newMobilities.Add(match);
                            newMobilities.Sort();
                            ionMobilitiesDict[matchedTarget] = newMobilities.ToArray();
                        }
                        else
                        {
                            ionMobilitiesDict[matchedTarget] = new[] {match};
                        }
                    }
                }
                if (!ionMobilitiesDict.Values.Any(v => v.Any()))
                {
                    ionMobilities = null;
                    return false;
                }
                ionMobilities = new LibraryIonMobilityInfo(FilePath, false, ionMobilitiesDict);
                return true;
            }

            return base.TryGetIonMobilityInfos(targetIons, out ionMobilities);
        }

        public override IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            if (FindEntry(key) >= 0)
            {
                var item = _libraryEntries.Index.ItemsMatching(key.LibraryKey, true).FirstOrDefault();
                _libraryEntries.TryGetValue(key, out var spectrumInfo);
                var files = LibraryDetails.DataFiles.ToList();
                var file = spectrumInfo.SampleFileId > 0 && spectrumInfo.SampleFileId <= files.Count ?
                    files[spectrumInfo.SampleFileId-1].FilePath :
                    null;
                yield return new SpectrumInfoLibrary(this, labelType, file,
                    null,  
                    spectrumInfo.IonMobility, 
                    null, true, item.OriginalIndex) 
                {
                    SpectrumHeaderInfo = CreateSpectrumHeaderInfo(spectrumInfo)
                };
            }
        }

        private bool Load(ILoadMonitor loader)
        {
            ProgressStatus status = new ProgressStatus(string.Format(ChromLibResources.ChromatogramLibrary_Load_Loading__0_, Name));
            loader.UpdateProgress(status);
            if (LoadFromCache(loader, status))
            {
                loader.UpdateProgress(status.Complete());
                return true;
            }
            if (LoadLibraryFromDatabase(loader))
            {
                using (var fileSaver = new FileSaver(CachePath, loader.StreamManager))
                {
                    using (var stream = loader.StreamManager.CreateStream(fileSaver.SafeName, FileMode.Create, true))
                    {
                        var serializer = new Serializer(this, stream);
                        serializer.Write();
                        loader.StreamManager.Finish(stream);
                        fileSaver.Commit();
                        loader.StreamManager.SetCache(FilePath, CachePath);
                    }
                }
                loader.UpdateProgress(status.Complete());
                return true;
            }
            return false;
        }

        private bool LoadLibraryFromDatabase(ILoadMonitor loader)
        {
            try
            {
                var status = new ProgressStatus(string.Format(Resources.ChromatogramLibrary_LoadLibraryFromDatabase_Reading_precursors_from__0_, Name));
                loader.UpdateProgress(status);
                //                _pooledSessionFactory = new PooledSessionFactory(loader.StreamManager.ConnectionPool,
//                                                                 typeof (ChromLibEntity), FilePath);
                using (var session = _pooledSessionFactory.Connection.OpenSession())
                {
                    var libInfo =
                        session.CreateSQLQuery(@"SELECT PanoramaServer, LibraryRevision, SchemaVersion FROM LibInfo")
                               .UniqueResult<object[]>();
                    PanoramaServer = Convert.ToString(libInfo[0]);
                    LibraryRevision = Convert.ToInt32(libInfo[1]);
                    SchemaVersion = Convert.ToString(libInfo[2]);
                    var hasSmallMoleculeAndIonMobilityColumns = Convert.ToDouble(libInfo[2], CultureInfo.InvariantCulture) >= 3;
                    var dictMolecules = new Dictionary<int, CustomMolecule>();
                    var dictMoleculeLists = new Dictionary<int, Protein>();
                    if (hasSmallMoleculeAndIonMobilityColumns)
                    {
                        var moleculeQuery = session.CreateQuery(@"SELECT Id FROM Peptide"); 
                        foreach (var peptideId in moleculeQuery.List<int>())
                        {
                            var peptide13 = GetPeptide(session, peptideId) as Data.Peptide.Format1Dot3;
                            if (peptide13 == null)
                            {
                                break;
                            }

                            var formula = peptide13.ChemicalFormula;
                            var name = peptide13.MoleculeName;
                            
                            if (string.IsNullOrEmpty(formula) && string.IsNullOrEmpty(name) && peptide13.MassAverage == 0)
                            {
                                continue; // Probably a peptide rather than a molecule
                            }
                            var accessionNumbers = MoleculeAccessionNumbers.FromSerializableString(peptide13.MoleculeAccession);

                            var molecule = formula == null ? 
                                new CustomMolecule(new TypedMass(peptide13.MassMonoisotopic, MassType.Monoisotopic), 
                                    new TypedMass(peptide13.MassAverage, MassType.Average), name, accessionNumbers) : 
                                new CustomMolecule(formula, name, accessionNumbers);
                            dictMolecules.Add(peptideId, molecule);
                            dictMoleculeLists.Add(peptideId, peptide13.Protein); // For small molecules "Protein" is really molecule list name
                        }
                    }
                    try
                    {
                        var irtQuery = session.CreateQuery(@"SELECT PeptideModSeq, Irt, TimeSource FROM IrtLibrary");
                        _libraryIrts = irtQuery.List<object[]>().Select(
                            irt => new ChromatogramLibraryIrt(Target.FromSerializableString((string) irt[0]), (TimeSource) irt[2], Convert.ToDouble(irt[1]))
                            ).ToArray();
                    }
                    catch (GenericADOException)
                    {
                        // IrtLibrary table probably doesn't exist
                    }

                    var rtQuery = session.CreateQuery(@"SELECT Precursor.Id, SampleFile.Id, RetentionTime FROM PrecursorRetentionTime");
                    var rtDictionary = new Dictionary<int, List<KeyValuePair<int, double>>>(); // PrecursorId -> [SampleFileId -> RetentionTime]
                    foreach (object[] row in rtQuery.List<object[]>())
                    {
                        var precursorId = (int) row[0];
                        var sampleFileId = (int) row[1];
                        var rt = Convert.ToDouble(row[2]);
                        if (!rtDictionary.ContainsKey(precursorId))
                        {
                            rtDictionary.Add(precursorId, new List<KeyValuePair<int, double>>());
                        }
                        rtDictionary[precursorId].Add(new KeyValuePair<int, double>(sampleFileId, rt));
                    }

                    var precursorQuery =
                        session.CreateQuery(@"SELECT P.Id, P.ModifiedSequence, P.Charge, P.TotalArea, P.PeptideId, P.SampleFileId FROM " + typeof (Precursor) +
                                            @" P");
                    var allTransitionAreas = ReadAllTransitionAreas(session, dictMolecules.Any());
                    var spectrumInfos = new List<ChromLibSpectrumInfo>();
                    foreach (object[] row in precursorQuery.List<object[]>())
                    {
                        var id = (int) row[0];
                        var precursor13 = hasSmallMoleculeAndIonMobilityColumns ? GetPrecursor(session, id) as Precursor.Format1Dot3 : null;
                        var adductSting = precursor13?.Adduct;
                        if ((row[1] == null || row[2] == null) && string.IsNullOrEmpty(adductSting))
                        {
                            continue; // Empty record? Throw an error?
                        }

                        var modifiedSequenceString = (string) row[1];
                        var peptideId = (int) row[4];
                        var moleculeTarget = hasSmallMoleculeAndIonMobilityColumns && !string.IsNullOrEmpty(adductSting) && string.IsNullOrEmpty(modifiedSequenceString) ?
                            new Target(dictMolecules[peptideId]) :
                            null;

                        var sampleFileId = (int) row[5];

                        LibKey libKey;
                        string moleculeList = null;  // CONSIDER(bspratt) pass protein name through as we do molecule list name?
                        if (moleculeTarget == null)
                        {
                            var modifiedSequence = new Target(modifiedSequenceString);
                            var charge = (int) row[2]; 
                            var modSeqNormal = SequenceMassCalc.NormalizeModifiedSequence(modifiedSequence);
                            libKey = new LibKey(modSeqNormal.Sequence, charge);
                        }
                        else
                        {
                            libKey = new LibKey(moleculeTarget, Adduct.FromStringAssumeChargeOnly(adductSting));
                            moleculeList = dictMoleculeLists[peptideId]?.Name;
                        }
                        double totalArea = Convert.ToDouble(row[3]);
                        List<KeyValuePair<int, double>> retentionTimes;
                        var indexedRetentionTimes = new IndexedRetentionTimes();
                        if (rtDictionary.TryGetValue(id, out retentionTimes))
                        {
                            indexedRetentionTimes = new IndexedRetentionTimes(retentionTimes);
                        }

                        // Note ion mobility, if any.
                        IonMobilityAndCCS ionMobility = precursor13?.GetIonMobilityAndCCS();

                        IList<SpectrumPeaksInfo.MI> transitionAreas;
                        allTransitionAreas.TryGetValue(id, out transitionAreas);
                        spectrumInfos.Add(new ChromLibSpectrumInfo(libKey, id, sampleFileId, totalArea, indexedRetentionTimes, ionMobility, transitionAreas, moleculeList));
                    }
                    SetLibraryEntries(spectrumInfos);

                    var sampleFileQuery =
                        session.CreateQuery(@"SELECT Id, FilePath, SampleName, AcquiredTime, ModifiedTime, InstrumentIonizationType, " +
                                            @"InstrumentAnalyzer, InstrumentDetector FROM SampleFile");
                    var sampleFiles = new List<ChromatogramLibrarySourceInfo>();
                    foreach (object[] row in sampleFileQuery.List<object[]>())
                    {
                        var id = (int) row[0];
                        if (row[1] == null || row[2] == null)
                        {
                            continue; // Throw an error?
                        }
                        var filePath = row[1].ToString();
                        var sampleName = row[2].ToString();
                        var acquiredTime = row[3] != null ? row[3].ToString() : string.Empty;
                        var modifiedTime = row[4] != null ? row[4].ToString() : string.Empty;
                        var instrumentIonizationType = row[5] != null ? row[5].ToString() : string.Empty;
                        var instrumentAnalyzer = row[6] != null ? row[6].ToString() : string.Empty;
                        var instrumentDetector = row[7] != null ? row[7].ToString() : string.Empty;
                        sampleFiles.Add(new ChromatogramLibrarySourceInfo(id, filePath, sampleName, acquiredTime, modifiedTime, instrumentIonizationType,
                                                                          instrumentAnalyzer, instrumentDetector));
                    }
                    _librarySourceFiles = sampleFiles.ToArray();

                    loader.UpdateProgress(status.Complete());
                    return true;
                }
            }
            catch (Exception e)
            {
                Messages.WriteAsyncUserMessage(ChromLibResources.ChromatogramLibrary_LoadLibraryFromDatabase_Error_loading_chromatogram_library__0_, e);
                return false;
            }
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            int i = FindEntry(key);
            int j = _librarySourceFiles.IndexOf(info => Equals(filePath.ToString(), info.FilePath));
            if (i != -1 && j != -1)
            {
                retentionTimes = _libraryEntries[i].RetentionTimesByFileId.GetTimes(_librarySourceFiles[j].Id);
                return true;
            }

            return base.TryGetRetentionTimes(key, filePath, out retentionTimes);
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            int j = _librarySourceFiles.IndexOf(info => Equals(filePath.ToString(), info.FilePath));
            if (j != -1)
            {
                var source = _librarySourceFiles[j];
                ILookup<Target, double[]> timesLookup = _libraryEntries.ToLookup(
                    entry => entry.Key.Target,
                    entry => entry.RetentionTimesByFileId.GetTimes(source.Id));
                var timesDict = timesLookup.ToDictionary(
                    grouping => grouping.Key,
                    grouping =>
                    {
                        var array = grouping.SelectMany(values => values).ToArray();
                        Array.Sort(array);
                        return array;
                    });
                var nonEmptyTimesDict = timesDict
                    .Where(kvp => kvp.Value.Length > 0)
                    .ToDictionary(kvp => kvp.Key, kvp => new Tuple<TimeSource, double[]>(TimeSource.peak, kvp.Value));

                retentionTimes = new LibraryRetentionTimes(filePath.ToString(), nonEmptyTimesDict);
                return true;
            }

            return base.TryGetRetentionTimes(filePath, out retentionTimes);
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            return TryGetRetentionTimes(MsDataFileUri.Parse(_librarySourceFiles[fileIndex].FilePath), out retentionTimes);
        }

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            if (_libraryIrts == null || !_libraryIrts.Any())
            {
                retentionTimes = null;
                return false;
            }

            var irtDictionary = new Dictionary<Target, Tuple<TimeSource, double[]>>();
            foreach (var irt in _libraryIrts)
            {
                if (!irtDictionary.ContainsKey(irt.Sequence))
                {
                    irtDictionary[irt.Sequence] = new Tuple<TimeSource, double[]>(irt.TimeSource, new []{irt.Irt});
                }
            }
            retentionTimes = new LibraryRetentionTimes(null, irtDictionary);
            return true;
        }

        /// <summary>
        /// Returns a mapping from PrecursorId to list of mz/intensities.
        /// </summary>
        private IDictionary<int, IList<SpectrumPeaksInfo.MI>> ReadAllTransitionAreas(ISession session, bool checkPeakAnnotations)
        {
            var allPeakAreas = new Dictionary<int, IList<SpectrumPeaksInfo.MI>>();
            var query =
                session.CreateQuery(@"SELECT T.Precursor.Id as First, T.Mz, T.Area, T.Id FROM " + typeof(Data.Transition) + @" T");
            var rows = query.List<object[]>();
            var rowsLookup = rows.ToLookup(row => (int) (row[0]));
            foreach (var grouping in rowsLookup)
            {
                var mis = ImmutableList.ValueOf(grouping.Select(row
                    => new SpectrumPeaksInfo.MI
                    {
                        Mz = Convert.ToDouble(row[1]), Intensity = Convert.ToSingle(row[2]),
                        Annotations = checkPeakAnnotations ? GetAnnotations(session, (int)row[3]) : null }));
                allPeakAreas.Add(grouping.Key, mis);
            }
            return allPeakAreas;
        }

        private List<SpectrumPeakAnnotation> GetAnnotations(ISession session, int id)
        {
            var transition13 = GetTransition(session, id) as Data.Transition.Format1Dot3;
            if (!string.IsNullOrEmpty(transition13?.FragmentName) || !string.IsNullOrEmpty(transition13?.ChemicalFormula))
            {
                var ion = new CustomIon(transition13.ChemicalFormula, transition13.GetAdduct(), null, null, transition13.FragmentName);
                return new List<SpectrumPeakAnnotation>{SpectrumPeakAnnotation.Create(ion, null)};
            }
            return null;
        }

        private bool LoadFromCache(ILoadMonitor loadMonitor, ProgressStatus status)
        {
            if (!loadMonitor.StreamManager.IsCached(FilePath, CachePath))
            {
                return false;
            }
            try
            {
                using (var stream = loadMonitor.StreamManager.CreateStream(CachePath, FileMode.Open, true))
                {
                    var serializer = new Serializer(this, stream);
                    serializer.Read();
                    return true;
                }
            }
            catch (Exception exception)
            {
                Messages.WriteAsyncUserMessage(ChromLibResources.ChromatogramLibrary_LoadFromCache_Exception_reading_cache__0_, exception);
                return false;
            }
        }

        public static ChromatogramLibrary LoadFromDatabase(ChromatogramLibrarySpec chromatogramLibrarySpec, ILoadMonitor loadMonitor)
        {
            var library = new ChromatogramLibrary(chromatogramLibrarySpec, loadMonitor.StreamManager);
            if (library.Load(loadMonitor))
                return library;
            return null;
        }
        private ChromatogramLibrary()
        {
            _librarySourceFiles = new ChromatogramLibrarySourceInfo[0];
        }
        private enum ATTR
        {
            panorama_server,
            revision
        }
        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            PanoramaServer = reader.GetAttribute(ATTR.panorama_server);
            LibraryRevision = reader.GetIntAttribute(ATTR.revision);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.panorama_server, PanoramaServer);
            writer.WriteAttribute(ATTR.revision, LibraryRevision);
        }

        public static ChromatogramLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ChromatogramLibrary());
        }

        protected bool Equals(ChromatogramLibrary other)
        {
            return base.Equals(other) && string.Equals(FilePath, other.FilePath) && string.Equals(PanoramaServer, other.PanoramaServer) && LibraryRevision == other.LibraryRevision;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ChromatogramLibrary) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (PanoramaServer != null ? PanoramaServer.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ LibraryRevision;
                return hashCode;
            }
        }

        private struct ChromatogramLibrarySourceInfo
        {
            public ChromatogramLibrarySourceInfo(int id, string filePath, string sampleName, string acquiredTime, string modifiedTime,
                string instrumentIonizationType, string instrumentAnalyzer, string instrumentDetector)
                : this()
            {
                Id = id;
                FilePath = filePath;
                SampleName = sampleName;
                AcquiredTime = acquiredTime;
                ModifiedTime = modifiedTime;
                InstrumentIonizationType = instrumentIonizationType;
                InstrumentAnalyzer = instrumentAnalyzer;
                InstrumentDetector = instrumentDetector;
            }

            public int Id { get; private set; }
            public string FilePath { get; private set; }
            public string SampleName { get; private set; }
            public string AcquiredTime { get; private set; }
            public string ModifiedTime { get; private set; }
            public string InstrumentIonizationType { get; private set; }
            public string InstrumentAnalyzer { get; private set; }
            public string InstrumentDetector { get; private set; }

            public string BaseName
            {
                get
                {
                    try
                    {
                        return Path.GetFileNameWithoutExtension(FilePath);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }

            public void Write(Stream stream)
            {
                PrimitiveArrays.WriteOneValue(stream, Id);
                WriteString(stream, FilePath);
                WriteString(stream, SampleName);
                WriteString(stream, AcquiredTime);
                WriteString(stream, ModifiedTime);
                WriteString(stream, InstrumentIonizationType);
                WriteString(stream, InstrumentAnalyzer);
                WriteString(stream, InstrumentDetector);
            }

            public static ChromatogramLibrarySourceInfo Read(Stream stream)
            {
                int id = PrimitiveArrays.ReadOneValue<int>(stream);
                string filePath = ReadString(stream);
                string sampleName = ReadString(stream);
                string acquiredTime = ReadString(stream);
                string modifiedTime = ReadString(stream);
                string instrumentIonizationType = ReadString(stream);
                string instrumentAnalyzer = ReadString(stream);
                string instrumentDetector = ReadString(stream);
                return new ChromatogramLibrarySourceInfo(id, filePath, sampleName, acquiredTime, modifiedTime, instrumentIonizationType,
                                                         instrumentAnalyzer, instrumentDetector);
            }

            private static void WriteString(Stream stream, string str)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                PrimitiveArrays.WriteOneValue(stream, bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string ReadString(Stream stream)
            {
                int byteLength = PrimitiveArrays.ReadOneValue<int>(stream);
                var bytes = new byte[byteLength];
                stream.ReadOrThrow(bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        private struct ChromatogramLibraryIrt
        {
            public ChromatogramLibraryIrt(Target seq, TimeSource timeSource, double irt)
                : this()
            {
                Sequence = seq;
                TimeSource = timeSource;
                Irt = irt;
            }

            public Target Sequence { get; private set; }
            public TimeSource TimeSource { get; private set; }
            public double Irt { get; private set; }

            public void Write(Stream stream)
            {
                WriteString(stream, Sequence.ToSerializableString());
                PrimitiveArrays.WriteOneValue(stream, (int)TimeSource);
                PrimitiveArrays.WriteOneValue(stream, Irt);
            }

            public static ChromatogramLibraryIrt Read(Stream stream)
            {
                var seq = Target.FromSerializableString(ReadString(stream));
                var timeSource = (TimeSource)PrimitiveArrays.ReadOneValue<int>(stream);
                var irt = PrimitiveArrays.ReadOneValue<double>(stream);
                return new ChromatogramLibraryIrt(seq, timeSource, irt);
            }

            private static void WriteString(Stream stream, string str)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                PrimitiveArrays.WriteOneValue(stream, bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string ReadString(Stream stream)
            {
                int byteLength = PrimitiveArrays.ReadOneValue<int>(stream);
                var bytes = new byte[byteLength];
                stream.ReadOrThrow(bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
        }

        private class Serializer
        {
            // Version 5 adds small molecule and ion mobility information
            private const int CURRENT_VERSION = 5;
            private const int MIN_READABLE_VERSION = 5;

            private readonly ChromatogramLibrary _library;
            private readonly Stream _stream;
            private long _locationEntries;
            private long _locationHeader;
            
            public Serializer(ChromatogramLibrary library, Stream stream)
            {
                _library = library;
                _stream = stream;
            }

            public void Read()
            {
                ReadHeader();
                ReadEntries();
            }

            public void Write()
            {
                WriteEntries();
                WriteHeader();
            }

            private void WriteEntries()
            {
                _locationEntries = _stream.Position;
                PrimitiveArrays.WriteOneValue(_stream, _library._libraryEntries.Length);
                foreach (var entry in _library._libraryEntries)
                {
                    entry.Write(_stream);
                }
                PrimitiveArrays.WriteOneValue(_stream, _library._libraryIrts.Length);
                foreach (var entry in _library._libraryIrts)
                {
                    entry.Write(_stream);
                }
            }
            private void ReadEntries()
            {
                var valueCache = new ValueCache();
                _stream.Seek(_locationEntries, SeekOrigin.Begin);
                int entryCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                var entries = new ChromLibSpectrumInfo[entryCount];
                for (int i = 0; i < entryCount; i++)
                {
                    entries[i] = ChromLibSpectrumInfo.Read(valueCache, _stream);
                }
                _library.SetLibraryEntries(entries);
                int irtCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                _library._libraryIrts = new ChromatogramLibraryIrt[irtCount];
                for (int i = 0; i < irtCount; i++)
                {
                    _library._libraryIrts[i] = ChromatogramLibraryIrt.Read(_stream);
                }
            }
            private void WriteHeader()
            {
                _locationHeader = _stream.Position;
                PrimitiveArrays.WriteOneValue(_stream, CURRENT_VERSION);
                PrimitiveArrays.WriteOneValue(_stream, _locationEntries);
                WriteString(_stream, _library.PanoramaServer);
                PrimitiveArrays.WriteOneValue(_stream, _library.LibraryRevision);
                WriteString(_stream, _library.SchemaVersion);
                PrimitiveArrays.WriteOneValue(_stream, _library._librarySourceFiles.Length);
                foreach (var sampleFile in _library._librarySourceFiles)
                {
                    sampleFile.Write(_stream);
                }
                PrimitiveArrays.WriteOneValue(_stream, _locationEntries);
                PrimitiveArrays.WriteOneValue(_stream, _locationHeader);
            }

            private void ReadHeader()
            {
                _stream.Seek(-sizeof (long), SeekOrigin.End);
                _locationHeader = PrimitiveArrays.ReadOneValue<long>(_stream);
                _stream.Seek(_locationHeader, SeekOrigin.Begin);
                int version = PrimitiveArrays.ReadOneValue<int>(_stream);
                if (version > CURRENT_VERSION || version < MIN_READABLE_VERSION)
                {
                    throw new InvalidDataException(string.Format(ChromLibResources.Serializer_ReadHeader_Unsupported_file_version__0_, version));
                }
                _locationEntries = PrimitiveArrays.ReadOneValue<long>(_stream);
                _library.PanoramaServer = ReadString(_stream);
                _library.LibraryRevision = PrimitiveArrays.ReadOneValue<int>(_stream);
                _library.SchemaVersion = ReadString(_stream);
                var sampleFileCount = PrimitiveArrays.ReadOneValue<int>(_stream);
                var sampleFiles = new List<ChromatogramLibrarySourceInfo>();
                for (int i = 0; i < sampleFileCount; i++)
                {
                    sampleFiles.Add(ChromatogramLibrarySourceInfo.Read(_stream));
                }
                _library._librarySourceFiles = sampleFiles.ToArray();
                _locationEntries = PrimitiveArrays.ReadOneValue<long>(_stream);
            }

            private static void WriteString(Stream stream, string str)
            {
                var bytes = Encoding.UTF8.GetBytes(str);
                PrimitiveArrays.WriteOneValue(stream, bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }

            private static string ReadString(Stream stream)
            {
                int byteLength = PrimitiveArrays.ReadOneValue<int>(stream);
                var bytes = new byte[byteLength];
                stream.ReadOrThrow(bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
