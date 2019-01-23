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
using System.Threading;
using NHibernate;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
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

    public class IonMobilityDb : Immutable, IValidating
    {
        public const string EXT = ".imdb";

        public static string FILTER_IONMOBILITYLIBRARY
        {
            get { return TextUtil.FileDialogFilter(Resources.IonMobilityDb_FILTER_IONMOBILITYLIBRARY_Ion_Mobility_Library_Files, EXT); }
        }

        public const int SCHEMA_VERSION_CURRENT = 3; // Version 2 adds high energy drift time offset, version 3 adds adduct and small molecule info

        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;
        private int _schemaVersion;

        private DateTime _modifiedTime;
        private ImmutableDictionary<LibKey, DbIonMobilityPeptide> _dictLibrary;

        private IonMobilityDb(String path, ISessionFactory sessionFactory)
        {
            _path = path;
            _sessionFactory = sessionFactory;
            _databaseLock = new ReaderWriterLock();
            // Do we need to update the db to current version?
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                ReadVersion(session);
            }
            if (_schemaVersion < SCHEMA_VERSION_CURRENT)
            {
                using (var session = OpenWriteSession())
                {
                    UpdateSchema(session);
                }
            }

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

        private IDictionary<LibKey, DbIonMobilityPeptide> DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = new ImmutableDictionary<LibKey, DbIonMobilityPeptide>(value); }
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        // TODO(bspratt) either upgrade this for all ion mobility types, or rip out this code altogether
        public IonMobilityAndCCS GetDriftTimeInfo(LibKey key, ChargeRegressionLine regression)
        {
            DbIonMobilityPeptide pep;
            if (DictLibrary.TryGetValue(key, out pep))
                return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(regression.GetY(pep.CollisionalCrossSection), eIonMobilityUnits.drift_time_msec), pep.CollisionalCrossSection, pep.HighEnergyDriftTimeOffsetMsec);
            return null;
        }

        public IEnumerable<DbIonMobilityPeptide> GetPeptides()
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                LoadPeptides(session.CreateCriteria(typeof (DbIonMobilityPeptide)).List<DbIonMobilityPeptide>());
                return DictLibrary.Values;
            }
        }

        #region Property change methods

        private IonMobilityDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            var result = ChangeProp(ImClone(this), im => im.LoadPeptides(im.GetPeptides()));
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public IonMobilityDb UpdatePeptides(IList<ValidatingIonMobilityPeptide> newPeptides, IList<ValidatingIonMobilityPeptide> oldPeptides)
        {
            var dictOld = new Dictionary<LibKey, ValidatingIonMobilityPeptide>();
            foreach (var ionMobilityPeptide in oldPeptides)  // Not using ToDict in case of duplicate entries
            {
                ValidatingIonMobilityPeptide pep;
                var libKey = ionMobilityPeptide.GetLibKey();
                if (!dictOld.TryGetValue(libKey, out pep))
                    dictOld[libKey] = ionMobilityPeptide;
            }
            var dictNew = new Dictionary<LibKey, ValidatingIonMobilityPeptide>();
            foreach (var ionMobilityPeptide in newPeptides)  // Not using ToDict in case of duplicate entries
            {
                ValidatingIonMobilityPeptide pep;
                var libKey = ionMobilityPeptide.GetLibKey();
                if (!dictNew.TryGetValue(libKey, out pep))
                    dictNew[libKey] = ionMobilityPeptide;
            }

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                // Remove peptides that are no longer in the list
                foreach (var peptideOld in oldPeptides)
                {
                    ValidatingIonMobilityPeptide pep;
                    var libKey = peptideOld.GetLibKey();
                    if (!dictNew.TryGetValue(libKey, out pep))
                        session.Delete(peptideOld);
                }

                // Add or update peptides that have changed from the old list
                foreach (var peptideNew in newPeptides)
                {
                    ValidatingIonMobilityPeptide peptideOld;
                    // Create a new instance, because not doing this causes a BindingSource leak
                    var peptideNewDisconnected = new DbIonMobilityPeptide(peptideNew);
                    if (dictOld.TryGetValue(peptideNew.GetLibKey(), out peptideOld))
                    {
                        if (Equals(peptideNew, peptideOld))
                            continue;
                        session.SaveOrUpdate(peptideNewDisconnected);
                    }
                    else
                    {
                        session.Save(peptideNewDisconnected);
                    }
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.LoadPeptides(newPeptides));
        }

        private void LoadPeptides(IEnumerable<DbIonMobilityPeptide> peptides)
        {
            var dictLibrary = new Dictionary<LibKey, DbIonMobilityPeptide>();

            foreach (var pep in peptides)
            {
                var dict = dictLibrary;
                try
                {
                    DbIonMobilityPeptide ignored;
                    var adduct = pep.GetPrecursorAdduct();
                    if (adduct.IsEmpty)
                    {
                        // Older formats didn't consider charge to be a factor is CCS, so just fake up M+H, M+2H and M+3H
                        for (int z = 1; z <= 3; z++)
                        {
                            var newPep = new DbIonMobilityPeptide(pep.GetNormalizedModifiedSequence(),
                                Adduct.FromChargeProtonated(z), pep.CollisionalCrossSection, pep.HighEnergyDriftTimeOffsetMsec);
                            var key = newPep.GetLibKey();
                            if (!dict.TryGetValue(key, out ignored))
                                dict.Add(key, newPep);
                        }
                    }
                    else
                    {
                        var key = pep.GetLibKey();
                        if (!dict.TryGetValue(key, out ignored))
                            dict.Add(key, pep);
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            DictLibrary = dictLibrary;
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

        public static IonMobilityDb CreateIonMobilityDb(string path)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(IonMobilityDb), true))
            {
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(new DbVersionInfo { SchemaVersion = SCHEMA_VERSION_CURRENT });
                    transaction.Commit();
                }
            }

            return GetIonMobilityDb(path, null);
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

        private void ReadVersion(ISession session)
        {
            using (var cmd = session.Connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT SchemaVersion FROM VersionInfo";
                var obj = cmd.ExecuteScalar();
                _schemaVersion = Convert.ToInt32(obj);
            }
        }

        private void UpdateSchema(ISession session)
        {
            ReadVersion(session);  // Recheck version, in case another thread got here before us
            if ((_schemaVersion < SCHEMA_VERSION_CURRENT))
            {
                using (var transaction = session.BeginTransaction())
                using (var command = session.Connection.CreateCommand())
                {
                    if (_schemaVersion < 2)
                    {
                        command.CommandText =
                            @"ALTER TABLE IonMobilityLibrary ADD COLUMN HighEnergyDriftTimeOffsetMsec DOUBLE";
                        command.ExecuteNonQuery();
                    }
                    if (_schemaVersion < 3)
                    {
                        foreach (var col in new[] { @"PrecursorAdduct", @"MoleculeName", @"ChemicalFormula", @"InChiKey", @"OtherKeys" })
                        {
                            command.CommandText =
                                string.Format(@"ALTER TABLE IonMobilityLibrary ADD COLUMN {0} TEXT", col);
                            command.ExecuteNonQuery();
                        }
                    }
                    _schemaVersion = SCHEMA_VERSION_CURRENT;
                    command.CommandText = string.Format(@"UPDATE VersionInfo SET SchemaVersion = {0}", _schemaVersion);
                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
            }
            // else unhandled schema version update - let downstream process issue detailed exceptions about missing fields etc
        }
    }
}
