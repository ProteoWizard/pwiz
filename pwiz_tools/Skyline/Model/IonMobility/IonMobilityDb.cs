/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Threading;
using NHibernate;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.IonMobility
{
    public class DatabaseOpeningException : CalculatorException
    {
        public DatabaseOpeningException(string message) : base(message)
        {
        }

        public DatabaseOpeningException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class DbLibInfo
    {
        public const int INITIAL_LIBRARY_REVISION = 1;
        public const int SCHEMA_VERSION_CURRENT = 1;
        public virtual string LibLSID { get; set; }
        public virtual string CreateTime { get; set; }
        /// <summary>
        /// Revision number of the library.  Libraries start at revision 1,
        /// and that number gets increased if more stuff is added to the library.
        /// </summary>
        public virtual int MajorVersion { get; set; }
        /// <summary>
        /// Schema version of the library:
        /// Version 1 initial version
        /// </summary>
        public virtual int MinorVersion { get; set; }
    }

    public class IonMobilityDb : Immutable, IValidating, IDisposable
    {
        public const string EXT = ".imsdb";

        public static string FILTER_IONMOBILITYLIBRARY
        {
            get { return TextUtil.FileDialogFilter(Resources.IonMobilityDb_FILTER_IONMOBILITYLIBRARY_Ion_Mobility_Library_Files, EXT); }
        }


        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;

        // N.B. We allow more than one ion mobility per ion - this is the "multiple conformers" case (ion may have multiple shapes, thus multiple CCS)
        // LibKeyMap is a specialized dictionary class that can match modifications written at varying precisions
        private LibKeyMap<List<IonMobilityAndCCS>> _dictLibrary;

        private IonMobilityDb(string path, ISessionFactory sessionFactory)
        {
            _path = path;
            _sessionFactory = sessionFactory;
            _databaseLock = new ReaderWriterLock();
        }

        public void Validate()
        {
            // Set the modified time to the modified time for the database path
            try
            {
                _modifiedTime = File.GetLastWriteTime(_path);
            }
            catch (Exception)
            {
                _modifiedTime = new DateTime();
            }
        }

        public LibKeyMap<List<IonMobilityAndCCS>> DictLibrary
        {
            get { return _dictLibrary; }
            private set { _dictLibrary = value; }
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IList<IonMobilityAndCCS> GetIonMobilityInfo(LibKey key)
        {
            if (DictLibrary.TryGetValue(key, out var im) && im.Count > 0)
            {
                return im;
            }
            return null;
        }

        public IEnumerable<DbPrecursorAndIonMobility> GetIonMobilities()
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                return session.CreateCriteria(typeof (DbPrecursorAndIonMobility)).List<DbPrecursorAndIonMobility>();
            }
        }

        #region Property change methods

        private IonMobilityDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            var result = ChangeProp(ImClone(this), im => im.LoadIonMobilities());
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        /// <summary>
        /// Accepts a list of precursors with potentially multiple ion mobility values ("multiple conformers")
        /// and flattens it out into a list of potentially repeating precursors with different single IM values,
        /// which is how the .imdb format stores them.
        /// </summary>
        public IonMobilityDb UpdateIonMobilities(IEnumerable<PrecursorIonMobilities> newMobilities)
        {
            var list = new List<DbPrecursorAndIonMobility>();
            foreach (var pim in newMobilities)
            {
                foreach (var im in pim.IonMobilities)
                {
                    list.Add(new DbPrecursorAndIonMobility(new DbPrecursorIon(pim.Precursor),
                        im.CollisionalCrossSectionSqA, im.IonMobility.Mobility, im.IonMobility.Units, im.HighEnergyIonMobilityOffset));
                }
            }

            return UpdateIonMobilities(list);
        }

        public IonMobilityDb UpdateIonMobilities(IList<DbPrecursorAndIonMobility> newMobilities)
        {

            using (var session = OpenWriteSession())
            {
                var oldMoleculesSet = session.CreateCriteria<DbMolecule>().List<DbMolecule>();
                var oldPrecursorsSet = session.CreateCriteria<DbPrecursorIon>().List<DbPrecursorIon>();
                var oldMobilitiesSet = session.CreateCriteria<DbPrecursorAndIonMobility>().List<DbPrecursorAndIonMobility>();

                // Remove items that are no longer in the list
                foreach (var mobilityOld in oldMobilitiesSet)
                {
                    if (!newMobilities.Any(m => m.EqualsIgnoreId(mobilityOld)))
                    {
                        session.Delete(mobilityOld);
                        if (!newMobilities.Any(m => Equals(m.DbPrecursorIon, mobilityOld.DbPrecursorIon)))
                        {
                            session.Delete(mobilityOld.DbPrecursorIon);
                            if (!newMobilities.Any(m => Equals(m.DbPrecursorIon.DbMolecule, mobilityOld.DbPrecursorIon.DbMolecule)))
                            {
                                session.Delete(mobilityOld.DbPrecursorIon.DbMolecule);
                            }
                        }
                    }
                }

                // Add or update items that have changed from the old list
                var newMobilitiesSet = new HashSet<DbPrecursorAndIonMobility>();
                var newMoleculesSet = new HashSet<DbMolecule>();
                var newPrecursorsSet = new HashSet<DbPrecursorIon>();
                foreach (var itemNew in newMobilities)
                {
                    // Create a new instance, because not doing this causes a BindingSource leak
                    newMobilitiesSet.Add(new DbPrecursorAndIonMobility(itemNew));
                    newPrecursorsSet.Add(new DbPrecursorIon(itemNew.DbPrecursorIon));
                    newMoleculesSet.Add(new DbMolecule(itemNew.DbPrecursorIon.DbMolecule));
                }

                // Update the molecules table
                using (var transaction = session.BeginTransaction())
                {
                    foreach (var molecule in newMoleculesSet)
                    {
                        if (oldMoleculesSet.Any(m => m.EqualsIgnoreId(molecule)))
                        {
                            session.SaveOrUpdate(molecule);
                        }
                        else
                        {
                            session.Save(molecule);
                        }
                    }

                    transaction.Commit();
                }

                // Read them back to get their assigned IDs
                oldMoleculesSet = session.CreateCriteria<DbMolecule>().List<DbMolecule>();

                // Update the precursors table
                using (var transaction = session.BeginTransaction())
                {
                    foreach (var precursor in newPrecursorsSet)
                    {
                        var dbMoleculeWithId = oldMoleculesSet.FirstOrDefault(m => m.EqualsIgnoreId(precursor.DbMolecule));
                        var precursorWithMoleculeId =
                            new DbPrecursorIon(dbMoleculeWithId, precursor.GetPrecursorAdduct());
                        if (oldPrecursorsSet.Any(p => p.EqualsIgnoreId(precursor)))
                        {
                            session.SaveOrUpdate(precursorWithMoleculeId);
                        }
                        else
                        {
                            session.Save(precursorWithMoleculeId);
                        }
                    }

                    transaction.Commit();
                }

                // Read them back to get their assigned IDs
                oldPrecursorsSet = session.CreateCriteria<DbPrecursorIon>().List<DbPrecursorIon>();

                // Update the mobilities table
                using (var transaction = session.BeginTransaction())
                {

                    foreach (var mobility in newMobilitiesSet)
                    {
                        var dbPrecursorIonWithId = oldPrecursorsSet.FirstOrDefault(p => p.EqualsIgnoreId(mobility.DbPrecursorIon));
                        var mobilityWithPrecursorId = new DbPrecursorAndIonMobility(dbPrecursorIonWithId,
                            mobility.CollisionalCrossSectionSqA, mobility.IonMobilityNullable,
                            mobility.IonMobilityUnits, mobility.HighEnergyIonMobilityOffset);
                        if (oldMobilitiesSet.Any(m => m.EqualsIgnoreId(mobility)))
                        {
                            session.SaveOrUpdate(mobilityWithPrecursorId);
                        }
                        else
                        {
                            session.Save(mobilityWithPrecursorId);
                        }
                    }

                    transaction.Commit();
                }
            }

            return ChangeProp(ImClone(this), im => im.LoadIonMobilities());
        }

        /// <summary>
        /// Take the current list of DbIonMobilityValues (which may have multiple occurrences of
        /// a precursor ion, implying multiple conformers for that ion) and convert it
        /// to a dictionary of precursor ions and their (possibly multiple) ion mobilities
        /// </summary>
        private void LoadIonMobilities()
        {

            var dictLibrary = new Dictionary<LibKey, List<IonMobilityAndCCS>>();

            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                var ionMobilities = session.CreateCriteria(typeof(DbPrecursorAndIonMobility)).List<DbPrecursorAndIonMobility>();

                foreach (var im in ionMobilities)
                {
                    var dict = dictLibrary;
                    try
                    {
                        var key = im.DbPrecursorIon.GetLibKey();
                        var ionMobilityAndCCS = im.GetIonMobilityAndCCS();
                        if (!dict.TryGetValue(key, out var list))
                        {
                            dict.Add(key, new List<IonMobilityAndCCS>() {ionMobilityAndCCS});
                        }
                        else
                        {
                            list.Add(ionMobilityAndCCS);
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            DictLibrary = LibKeyMap<List<IonMobilityAndCCS>>.FromDictionary(dictLibrary);
        }

        #endregion

        #region object overrides

        public bool Equals(IonMobilityDb other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other._path, _path) &&
                other._modifiedTime.Equals(_modifiedTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IonMobilityDb)) return false;
            return Equals((IonMobilityDb) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_path.GetHashCode()*397) ^ _modifiedTime.GetHashCode();
            }
        }

        #endregion

        public static IonMobilityDb CreateIonMobilityDb(string path, string libraryName, bool minimized)
        {
            const string libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
            const int majorVer = 1;
            const int minorVer = DbLibInfo.SCHEMA_VERSION_CURRENT;
            //CONSIDER(bspratt): some better means of showing provenance of values in library?
            string libLsid = string.Format(@"urn:lsid:{0}:ion_mobility_library:skyline:{1}{2}:{3}:{4}.{5}",
                libAuthority, 
                minimized?@"minimal:":string.Empty,
                libraryName, Guid.NewGuid(), majorVer, minorVer);
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(IonMobilityDb), true))
            using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
            using (var transaction = session.BeginTransaction())
            {
                var createTime = new TimeStampISO8601().ToString();
                DbLibInfo libInfo = new DbLibInfo
                {
                    LibLSID = libLsid,
                    CreateTime = createTime,
                    MajorVersion = majorVer,
                    MinorVersion = minorVer
                };

                session.Save(libInfo);
                session.Flush();
                session.Clear();
                transaction.Commit();
            }

            return GetIonMobilityDb(path, null);

        }

        public static IonMobilityDb CreateIonMobilityDb(string path, string libraryName, bool minimized, IList<PrecursorIonMobilities> peptides)
        {
            var db = CreateIonMobilityDb(path, libraryName, minimized);
            return db.UpdateIonMobilities(peptides);
        }

        /// <summary>
        /// Maintains a single string globally for each path, in order to keep from
        /// having two threads accessing the same database at the same time.
        /// </summary>
        private static ISessionFactory GetSessionFactory(string path)
        {
            return SessionFactoryFactory.CreateSessionFactory(path, typeof(IonMobilityDb), false);
        }

        // Throws DatabaseOpeningException
        public static IonMobilityDb GetIonMobilityDb(string path, IProgressMonitor loadMonitor)
        {
            var status = new ProgressStatus(string.Format(Resources.IonMobilityDb_GetIonMobilityDb_Loading_ion_mobility_library__0_, path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (String.IsNullOrEmpty(path))
                    throw new DatabaseOpeningException(Resources.IonMobilityDb_GetIonMobilityDb_Please_provide_a_path_to_an_existing_ion_mobility_library_);
                if (!File.Exists(path))
                    throw new DatabaseOpeningException(
                        string.Format(
                        Resources.IonMobilityDb_GetIonMobilityDb_The_ion_mobility_library_file__0__could_not_be_found__Perhaps_you_did_not_have_sufficient_privileges_to_create_it_,
                        path));

                string message;
                Exception xInner = null;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    using (var sessionFactory = GetSessionFactory(path))
                    {
                        lock (sessionFactory)
                        {
                            return new IonMobilityDb(path, sessionFactory).Load(loadMonitor, status);
                        }
                    }
                }
                catch (UnauthorizedAccessException x)
                {
                    message = string.Format(Resources.IonMobilityDb_GetIonMobilityDb_You_do_not_have_privileges_to_access_the_ion_mobility_library_file__0_, path);
                    xInner = x;
                }
                catch (DirectoryNotFoundException x)
                {
                    message = string.Format(Resources.IonMobilityDb_GetIonMobilityDb_The_path_containing_ion_mobility_library__0__does_not_exist_, path);
                    xInner = x;
                }
                catch (FileNotFoundException x)
                {
                    message = string.Format(Resources.IonMobilityDb_GetIonMobilityDb_The_ion_mobility_library_file__0__could_not_be_found__Perhaps_you_did_not_have_sufficient_privileges_to_create_it_, path);
                    xInner = x;
                }
                catch (Exception x) // SQLiteException is already something of a catch-all, just lump it with the others here
                {
                    message = string.Format(Resources.IonMobilityDb_GetIonMobilityDb_The_file__0__is_not_a_valid_ion_mobility_library_file_, path);
                    xInner = x;
                }

                throw new DatabaseOpeningException(message, xInner);
            }
            catch (DatabaseOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                return null;
            }
        }

        public void Dispose()
        {
            _sessionFactory?.Dispose();
        }
    }
}
