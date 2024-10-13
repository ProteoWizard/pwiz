/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using NHibernate;
using NHibernate.Exceptions;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Optimization
{
    public class OptimizationsOpeningException : Exception
    {
        public OptimizationsOpeningException(string message)
            : base(message)
        {
        }
    }

    public class OptimizationDb : Immutable, IValidating
    {
        public const string EXT = ".optdb";

        public static string FILTER_OPTDB
        {
            get { return TextUtil.FileDialogFilter(OptimizationResources.OptimizationDb_FILTER_OPTDB_Optimization_Libraries, EXT); }
        }

        public const int SCHEMA_VERSION_CURRENT = 3; // Version 3 saves Charge and ProductCharge as TEXT instead of INT to accomodate adduct descriptions
                                                     // No special code needed for reading V2, SQLite is happy to present int as string

        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private DateTime _modifiedTime;
        private OptimizationDictionary _dictLibrary;

        public OptimizationDb(string path, ISessionFactory sessionFactory)
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

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<DbOptimization> GetOptimizations()
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                return session.CreateCriteria(typeof(DbOptimization)).List<DbOptimization>();
            }
        }

        #region Property change methods

        private OptimizationDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            var result = ChangeProp(ImClone(this), im => im.LoadOptimizations(im.GetOptimizations()));
            // Not really possible to show progress, unless we switch to raw reading
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
            return result;
        }

        public OptimizationDb UpdateOptimizations(IList<DbOptimization> newOptimizations, IList<DbOptimization> oldOptimizations)
        {
            var setNew = new HashSet<long>(newOptimizations.Select(opt => opt.Id ?? 0));
            var dictOld = new Dictionary<OptimizationKey, DbOptimization>();
            foreach (var opt in oldOptimizations)
            {
                if (!dictOld.ContainsKey(opt.Key))
                    dictOld.Add(opt.Key, opt);
            }

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                // Remove optimizations that are no longer in the list
                foreach (var optimization in oldOptimizations)
                {
                    if (!optimization.Id.HasValue)
                        continue;

                    if (!setNew.Contains(optimization.Id.Value))
                        session.Delete(optimization);
                }

                // Add or update optimizations that have changed from the old list
                foreach (var optimization in newOptimizations)
                {
                    DbOptimization optimizationOld;
                    if (dictOld.TryGetValue(optimization.Key, out optimizationOld) &&
                            Equals(optimization, optimizationOld))
                        continue;

                    // Create a new instance, because not doing this causes a BindingSource leak
                    var optimizationNewDisconnected = new DbOptimization(optimization);
                    session.SaveOrUpdate(optimizationNewDisconnected);
                }

                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im => im.LoadOptimizations(newOptimizations));
        }

        public OptimizationDictionary DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = value; }
        }

        private void LoadOptimizations(IEnumerable<DbOptimization> optimizations)
        {
            DictLibrary = new OptimizationDictionary(optimizations);
        }

        #endregion

        #region object overrides

        public bool Equals(OptimizationDb other)
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
            if (obj.GetType() != typeof(OptimizationDb)) return false;
            return Equals((OptimizationDb)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_path.GetHashCode() * 397) ^ _modifiedTime.GetHashCode();
            }
        }

        #endregion

        public static OptimizationDb CreateOptimizationDb(string path)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, typeof(OptimizationDb), true))
            {
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
                using (var transaction = session.BeginTransaction())
                {
                    session.Save(new DbVersionInfo { SchemaVersion = SCHEMA_VERSION_CURRENT });
                    transaction.Commit();
                }
            }

            return GetOptimizationDb(path, null, null);
        }

        /// <summary>
        /// Maintains a single string globally for each path, in order to keep from
        /// having two threads accessing the same database at the same time.
        /// </summary>
        private static ISessionFactory GetSessionFactory(string path)
        {
            return SessionFactoryFactory.CreateSessionFactory(path, typeof(OptimizationDb), false);
        }

        //Throws DatabaseOpeningException
        public static OptimizationDb GetOptimizationDb(string path, IProgressMonitor loadMonitor, SrmDocument document)
        {
            var status = new ProgressStatus(string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_Loading_optimization_library__0_, path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new OptimizationsOpeningException(OptimizationResources.OptimizationDb_GetOptimizationDb_Library_path_cannot_be_null_);

                if (!File.Exists(path))
                    throw new OptimizationsOpeningException(String.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_file__0__does_not_exist_, path));

                string message;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    //Allow only one thread at a time to read from the same path
                    using (var sessionFactory = GetSessionFactory(path))
                    {
                        lock (sessionFactory)
                        {
                            return new OptimizationDb(path, sessionFactory).Load(loadMonitor, status);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_You_do_not_have_privilieges_to_access_the_file__0__, path);
                }
                catch (DirectoryNotFoundException)
                {
                    message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_path_containing__0__does_not_exist_, path);
                }
                catch (FileNotFoundException)
                {
                    message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_file__0__could_not_be_created__Perhaps_you_do_not_have_sufficient_privileges_, path);
                }
                catch (SQLiteException)
                {
                    message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_file__0__is_not_a_valid_optimization_library_file_, path);
                }
                catch (GenericADOException)
                {
                    try
                    {
                        return ConvertFromOldFormat(path, loadMonitor, status, document);
                    }
                    catch (Exception e)
                    {
                        message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_file__0__could_not_be_opened__conversion_from_old_format_failed____1_, path, e.Message);
                    }
                }
                catch (Exception e)
                {
                    message = string.Format(OptimizationResources.OptimizationDb_GetOptimizationDb_The_file__0__could_not_be_opened___1_, path, e.Message);
                }

                throw new OptimizationsOpeningException(message);
            }
            catch (OptimizationsOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                return null;
            }
        }

        public static OptimizationDb ConvertFromOldFormat(string path, IProgressMonitor loadMonitor, ProgressStatus status, SrmDocument document)
        {
            // Try to open assuming old format (Id, PeptideModSeq, Charge, Mz, Value, Type)
            var precursors = new Dictionary<Target, HashSet<int>>(); // PeptideModSeq -> charges
            var optimizations = new List<Tuple<DbOptimization, double>>(); // DbOptimization, product m/z
            int maxCharge = 1;
            using (SQLiteConnection connection = new SQLiteConnection(@"Data Source = " + path))
            using (SQLiteCommand command = new SQLiteCommand(connection))
            {
                connection.Open();
                command.CommandText = @"SELECT PeptideModSeq, Charge, Mz, Value, Type FROM OptimizationLibrary";
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var type = (OptimizationType)reader[@"Type"];
                        var modifiedSequence = new Target(reader[@"PeptideModSeq"].ToString());
                        var charge = (int)reader[@"Charge"];
                        var productMz = (double)reader[@"Mz"];
                        var value = (double)reader[@"Value"];
                        optimizations.Add(new Tuple<DbOptimization, double>(new DbOptimization(type, modifiedSequence, Adduct.FromChargeProtonated(charge), string.Empty, Adduct.EMPTY, value), productMz));

                        if (!precursors.ContainsKey(modifiedSequence))
                        {
                            precursors[modifiedSequence] = new HashSet<int>();
                        }
                        precursors[modifiedSequence].Add(charge);
                        if (charge > maxCharge)
                        {
                            maxCharge = charge;
                        }
                    }
                }
            }
            var peptideList = (from precursor in precursors
                               from charge in precursor.Value
                               select string.Format(@"{0}{1}", precursor.Key, Transition.GetChargeIndicator(Adduct.FromChargeProtonated(charge)))
                               ).ToList();

            var newDoc = new SrmDocument(document != null ? document.Settings : SrmSettingsList.GetDefault());
            newDoc = newDoc.ChangeSettings(newDoc.Settings
                .ChangePeptideLibraries(libs => libs.ChangePick(PeptidePick.filter))
                .ChangeTransitionFilter(filter =>
                    filter.ChangeFragmentRangeFirstName(@"ion 1")
                          .ChangeFragmentRangeLastName(@"last ion")
                          .ChangePeptideProductCharges(Enumerable.Range(1, maxCharge).Select(Adduct.FromChargeProtonated).ToList()) // TODO(bspratt) negative charge peptides
                          .ChangePeptideIonTypes(new []{ IonType.y, IonType.b })) // TODO(bspratt) generalize to molecules?
                .ChangeTransitionLibraries(libs => libs.ChangePick(TransitionLibraryPick.none))
                );
            var matcher = new ModificationMatcher();
            matcher.CreateMatches(newDoc.Settings, peptideList, Settings.Default.StaticModList, Settings.Default.HeavyModList);
            FastaImporter importer = new FastaImporter(newDoc, matcher);
            // ReSharper disable LocalizableElement
            string text = TextUtil.LineSeparate(PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + newDoc.GetPeptideGroupId(true), TextUtil.LineSeparate(peptideList));
            // ReSharper restore LocalizableElement
            PeptideGroupDocNode imported = importer.Import(new StringReader(text), null, Helpers.CountLinesInString(text)).First();

            int optimizationsUpdated = 0;
            foreach (PeptideDocNode nodePep in imported.Children)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    var charge = nodeGroup.PrecursorAdduct;
                    var libKeyToMatch = newDoc.Settings.GetSourceTarget(nodePep).GetLibKey(charge).LibraryKey;
                    foreach (var nodeTran in nodeGroup.Transitions)
                    {
                        double productMz = nodeTran.Mz;
                        foreach (var optimization in optimizations.Where(opt =>
                            string.IsNullOrEmpty(opt.Item1.FragmentIon) &&
                            opt.Item1.ProductAdduct.IsEmpty &&  
                            Math.Abs(opt.Item2 - productMz) < 0.00001))
                        {
                            var optLibKey = optimization.Item1.Target.GetLibKey(optimization.Item1.Adduct).LibraryKey;
                            if (!LibKeyIndex.KeysMatch(optLibKey, libKeyToMatch))
                            {
                                continue;
                            }
                            optimization.Item1.FragmentIon = nodeTran.FragmentIonName;
                            optimization.Item1.ProductAdduct = nodeTran.Transition.Adduct;
                            ++optimizationsUpdated;
                        }
                    }
                }
            }

            if (optimizations.Count > optimizationsUpdated)
            {
                throw new OptimizationsOpeningException(string.Format(Resources.OptimizationDb_ConvertFromOldFormat_Failed_to_convert__0__optimizations_to_new_format_,
                                                                      optimizations.Count - optimizationsUpdated));
            }

            using (var fs = new FileSaver(path))
            {
                OptimizationDb db = CreateOptimizationDb(fs.SafeName);
                db.UpdateOptimizations(optimizations.Select(opt => opt.Item1).ToArray(), new DbOptimization[0]);
                fs.Commit();

                if (loadMonitor != null)
                    loadMonitor.UpdateProgress(status.ChangePercentComplete(100));
                return GetOptimizationDb(fs.RealName, null, null);
            }
        }
    }
}
