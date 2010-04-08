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
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NHibernate;
using NHibernate.Cfg;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class BlibDb
    {
        private static readonly Regex REGEX_LSID =
            new Regex("urn:lsid:([^:]*):spectral_library:bibliospec:[^:]*:([^:]*)");

        private BlibDb(String path)
        {
            FilePath = path;
            SessionFactory = SessionFactoryFactory.CreateSessionFactory(path, false);
            DatabaseLock = new ReaderWriterLock();
        }

        public ISession OpenSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, false);
        }

        public ISession OpenWriteSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, true);
        }

        public ReaderWriterLock DatabaseLock { get; private set; }
        public String FilePath { get; private set; }

        public void ConfigureMappings(Configuration configuration)
        {
            SessionFactoryFactory.ConfigureMappings(configuration);
        }

        private ISessionFactory SessionFactory { get; set; }
        public static BlibDb OpenBlibDb(String path)
        {
            return new BlibDb(path);
        }

        public static BlibDb CreateBlibDb(String path)
        {
            using (SessionFactoryFactory.CreateSessionFactory(path, true))
            {
            }
            return OpenBlibDb(path);
        }

        public int GetSpectraCount()
        {
            using (var session = OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery("SELECT Count(P.Id) From " + typeof(DbRefSpectra) + " P").UniqueResult());
            }
        }

        /// <summary>
        /// Minimize any library type to a fully functional BiblioSpec SQLite library.
        /// </summary>
        /// <param name="librarySpec">Library spec for which the new library is created</param>
        /// <param name="library">Existing library to minimize</param>
        /// <param name="document">Document for which only used spectra are included in the new library</param>
        /// <returns>A new minimized <see cref="BiblioSpecLiteLibrary"/></returns>
        public BiblioSpecLiteLibrary MinimizeLibrary(BiblioSpecLiteSpec librarySpec,
            Library library, SrmDocument document)
        {
            string libAuthority = "unknown.org";
            string libId = library.Name;
            // CONSIDER: Use version numbers of the original library?
            const int majorVer = 1;
            const int minorVer = 0;
            if (library is BiblioSpecLiteLibrary)
            {
                string libraryLsid = ((BiblioSpecLiteLibrary)library).Lsid;
                Match matchLsid = REGEX_LSID.Match(libraryLsid);
                if (matchLsid.Success)
                {
                    libAuthority = matchLsid.Groups[1].Value;
                    libId = matchLsid.Groups[2].Value;
                }
                else
                {
                    libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
                }
            }
            else if (library is BiblioSpecLibrary)
                libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
            else if (library is XHunterLibrary)
                libAuthority = XHunterLibrary.DEFAULT_AUTHORITY;
            else if (library is NistLibrary)
            {
                libAuthority = NistLibrary.DEFAULT_AUTHORITY;
                libId = ((NistLibrary)library).Id ?? libId;
            }
            // Use a very specific LSID, since it really only matches this document.
            string libLsid = string.Format("urn:lsid:{0}:spectral_libary:bibliospec:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), majorVer, minorVer);

            var dictLibrary = new Dictionary<LibKey, BiblioLiteSpectrumInfo>();

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                var settings = document.Settings;

                foreach (var nodePep in document.Peptides)
                {
                    var mods = nodePep.ExplicitMods;

                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        // Only get library info from precursors that use the desired library
                        if (!nodeGroup.HasLibInfo || !Equals(nodeGroup.LibInfo.LibraryName, library.Name))
                            continue;

                        TransitionGroup group = nodeGroup.TransitionGroup;
                        string peptideSeq = group.Peptide.Sequence;
                        int precursorCharge = group.PrecursorCharge;

                        SpectrumHeaderInfo headerInfo;
                        SpectrumPeaksInfo spectrumInfo;
                        IsotopeLabelType typeInfo;
                        if (settings.TryGetLibInfo(peptideSeq, precursorCharge, mods, out typeInfo, out headerInfo) &&
                            settings.TryLoadSpectrum(peptideSeq, precursorCharge, mods, out typeInfo, out spectrumInfo) &&
                            // Only if the library match is of the same type as the current group
                            typeInfo == nodeGroup.TransitionGroup.LabelType)
                        {
                            var calcPre = settings.GetPrecursorCalc(typeInfo, mods);
                            var peptideModSeq = calcPre.GetModifiedSequence(peptideSeq, false);
                            var libKey = new LibKey(peptideModSeq, precursorCharge);
                            if (dictLibrary.ContainsKey(libKey))
                                continue;

                            short copies = (short) headerInfo.GetRankValue(LibrarySpec.PEP_RANK_COPIES);
                            DbRefSpectra refSpectra = new DbRefSpectra
                                                          {
                                                              PeptideSeq = peptideSeq,
                                                              PrecursorMZ = nodeGroup.PrecursorMz,
                                                              PrecursorCharge = precursorCharge,
                                                              PeptideModSeq = peptideModSeq,
//                                                              NextAA = null,
//                                                              PrevAA = null,
                                                              Copies = copies,
                                                              NumPeaks = (short) spectrumInfo.Peaks.Length
                                                          };

                            refSpectra.Peaks = new DbRefSpectraPeaks
                                                   {
                                                       RefSpectra = refSpectra,
                                                       PeakIntensity = IntensitiesToBytes(spectrumInfo.Peaks),
                                                       PeakMZ = MZsToBytes(spectrumInfo.Peaks)
                                                   };

                            ModsFromModifiedSequence(refSpectra);

                            session.Save(refSpectra);

                            dictLibrary.Add(libKey,
                                new BiblioLiteSpectrumInfo(copies, refSpectra.NumPeaks, (int) refSpectra.Id));

                            session.Flush();
                            session.Clear();
                        }
                    }
                }

                // Simulate ctime(d), which is what BlibBuild uses.
                string createTime = string.Format("{0:ddd MMM dd HH:mm:ss yyyy}", DateTime.Now);
                DbLibInfo libInfo = new DbLibInfo
                                        {
                                            LibLSID = libLsid,
                                            CreateTime = createTime,
                                            NumSpecs = dictLibrary.Count,
                                            MajorVersion = majorVer,
                                            MinorVersion = minorVer
                                        };

                session.Save(libInfo);
                session.Flush();
                session.Clear();

                transaction.Commit();
            }

            return new BiblioSpecLiteLibrary(librarySpec, libLsid, majorVer, minorVer,
                dictLibrary, FileStreamManager.Default);
        }

        /// <summary>
        /// Reads modifications from a sequence with embedded modifications,
        /// e.g. AM[16.0]VLC[57.0]
        /// This results in some loss of precision, since embedded modifications
        /// are only accurate to one decimal place.  But this is all that is necessary
        /// for further use as spectral libraries for SRM method building.
        /// </summary>
        /// <param name="refSpectra"></param>
        private static void ModsFromModifiedSequence(DbRefSpectra refSpectra)
        {
            string modSeq = refSpectra.PeptideModSeq;
            for (int i = 0, iAa = 0; i < modSeq.Length; i++)
            {
                char c = modSeq[i];
                if (c != '[')
                    iAa++;
                else
                {
                    int iEnd = modSeq.IndexOf(']', ++i);
                    double modMass;
                    if (double.TryParse(modSeq.Substring(i, iEnd - i), out modMass))
                    {
                        if (refSpectra.Modifications == null)
                            refSpectra.Modifications = new List<DbModification>();

                        refSpectra.Modifications.Add(new DbModification
                                                         {
                                                             RefSpectra = refSpectra,
                                                             Mass = modMass,
                                                             Position = iAa
                                                         });
                        i = iEnd;
                        iAa++;
                    }
                }
            }
        }

        private static byte[] IntensitiesToBytes(SpectrumPeaksInfo.MI[] peaks)
        {
            const int sizeInten = sizeof(float);
            byte[] peakIntens = new byte[peaks.Length * sizeInten];
            for (int i = 0; i < peaks.Length; i++)
            {
                int offset = i*sizeInten;
                Array.Copy(BitConverter.GetBytes(peaks[i].Intensity), 0, peakIntens, offset, sizeInten);
            }
            return peakIntens.Compress();
        }

        private static byte[] MZsToBytes(SpectrumPeaksInfo.MI[] peaks)
        {
            const int sizeMz = sizeof(double);
            byte[] peakMZs = new byte[peaks.Length * sizeMz];
            for (int i = 0; i < peaks.Length; i++)
            {
                int offset = i * sizeMz;
                Array.Copy(BitConverter.GetBytes(peaks[i].Mz), 0, peakMZs, offset, sizeMz);
            }
            return peakMZs.Compress();
        }

        /// <summary>
        /// Minimizes all libraries in a document to produce a new document with
        /// just the library information necessary for the spectra referenced by
        /// the nodes in the document.
        /// </summary>
        /// <param name="document">Document for which to minimize library information</param>
        /// <param name="pathDirectory">Directory into which new minimized libraries are built</param>
        /// <param name="nameModifier">A name modifier to append to existing names for
        ///     full libraries to create new library names</param>
        /// <returns>A new document instance with minimized libraries</returns>
        public static SrmDocument MinimizeLibraries(SrmDocument document,
            string pathDirectory, string nameModifier)
        {
            var settings = document.Settings;
            var pepLibraries = settings.PeptideSettings.Libraries;
            if (!pepLibraries.HasLibraries)
                return document;
            if (!pepLibraries.IsLoaded)
                throw new InvalidOperationException("Libraries must be fully loaded before they can be minimzed.");

            // Separate group nodes by the libraries to which they refer
            var setUsedLibrarySpecs = new HashSet<LibrarySpec>();
            foreach (var librarySpec in pepLibraries.LibrarySpecs)
            {
                string libraryName = librarySpec.Name;
                if (document.TransitionGroups.Contains(nodeGroup =>
                        nodeGroup.HasLibInfo && Equals(nodeGroup.LibInfo.LibraryName, libraryName)))
                {
                    setUsedLibrarySpecs.Add(librarySpec);   
                }
            }

            var listLibraries = new List<Library>();
            var listLibrarySpecs = new List<LibrarySpec>();
            var dictOldNameToNew = new Dictionary<string, string>();
            if (setUsedLibrarySpecs.Count > 0)
            {
                Directory.CreateDirectory(pathDirectory);

                var usedNames = new HashSet<string>();
                for (int i = 0; i < pepLibraries.LibrarySpecs.Count; i++)
                {
                    var librarySpec = pepLibraries.LibrarySpecs[i];
                    if (!setUsedLibrarySpecs.Contains(librarySpec))
                        continue;

                    string baseName = Path.GetFileNameWithoutExtension(librarySpec.FilePath);
                    string fileName = GetUniqueName(baseName, usedNames) + BiblioSpecLiteSpec.EXT;
                    var blibDb = CreateBlibDb(Path.Combine(pathDirectory, fileName));
                    string nameMin = librarySpec.Name;
                    // Avoid adding the modifier a second time, if it has
                    // already been done once.
                    if (!nameMin.EndsWith(nameModifier + ")"))
                        nameMin = string.Format("{0} ({1})", librarySpec.Name, nameModifier);
                    var librarySpecMin = new BiblioSpecLiteSpec(nameMin, blibDb.FilePath);

                    listLibraries.Add(blibDb.MinimizeLibrary(librarySpecMin,
                        pepLibraries.Libraries[i], document));
                    listLibrarySpecs.Add(librarySpecMin);
                    dictOldNameToNew.Add(librarySpec.Name, librarySpecMin.Name);
                }

                document = (SrmDocument) document.ChangeAll(node =>
                    {
                        var nodeGroup = node as TransitionGroupDocNode;
                        if (nodeGroup == null || !nodeGroup.HasLibInfo)
                            return node;

                        string libName = nodeGroup.LibInfo.LibraryName;
                        var libInfo = nodeGroup.LibInfo.ChangeLibraryName(dictOldNameToNew[libName]);
                        return nodeGroup.ChangeLibInfo(libInfo);
                    },
                    (int) SrmDocument.Level.TransitionGroups);
            }

            return document.ChangeSettingsNoDiff(settings.ChangePeptideLibraries(
                lib => lib.ChangeLibraries(listLibrarySpecs, listLibraries)));
        }

        private static string GetUniqueName(string name, HashSet<string> usedNames)
        {
            if (usedNames.Contains(name))
            {
                // Append increasing number until a unique name is found
                string nameNew;
                int counter = 2;
                do
                {
                    nameNew = name + counter++;
                }
                while (usedNames.Contains(nameNew));
                name = nameNew;
            }
            usedNames.Add(name);
            return name;
        }
    }
}
