/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using System.IO;
using System.Threading;
using NHibernate;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.Util;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Irt
{
    public class DatabaseOpeningException : CalculatorException
    {
        public DatabaseOpeningException(string message)
            : base(message)
        {
        }
    }

    public class IrtDb : Immutable
    {
        public const string EXT_IRTDB = ".irtdb";

        public const int SCHEMA_VERSION_CURRENT = 1;

//        private readonly string _path;
        private readonly ISessionFactory _sessionFactory;
        private readonly ReaderWriterLock _databaseLock;

        private ImmutableDictionary<string, DbIrtPeptide> _dictStandards;
        private ImmutableDictionary<string, DbIrtPeptide> _dictLibrary;

        private IrtDb(String path)
        {
//            _path = path;
            _sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, false);
            _databaseLock = new ReaderWriterLock();
        }

        private IrtDb Load(IProgressMonitor loadMonitor, ProgressStatus status)
        {
            using (var session = new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, false))
            {
                var dictStandards = new Dictionary<string, DbIrtPeptide>();
                var dictLibrary = new Dictionary<string, DbIrtPeptide>();

                int totalRows = Convert.ToInt32(session.CreateSQLQuery("select count(*) from IrtLibrary").UniqueResult());
                int currentRow = 0;
                foreach (var pep in session.CreateCriteria(typeof(DbIrtPeptide)).List<DbIrtPeptide>())
                {
                    currentRow++;
                    int percent = currentRow * 100 / totalRows;
                    if (loadMonitor != null && status.PercentComplete != percent)
                    {
                        // Check for cancellation after each integer change in percent loaded.
                        if (loadMonitor.IsCanceled)
                        {
                            loadMonitor.UpdateProgress(status.Cancel());
                            return null;
                        }

                        // If not cancelled, update progress.
                        loadMonitor.UpdateProgress(status = status.ChangePercentComplete(percent));
                    }

                    var dict = pep.Standard ? dictStandards : dictLibrary;
                    dict.Add(pep.PeptideModSeq, pep);
                }

                DictStandards = dictStandards;
                DictLibrary = dictLibrary;
            }
            return this;
        }

        private IDictionary<string, DbIrtPeptide> DictStandards
        {
            get { return _dictStandards; }
            set { _dictStandards = new ImmutableDictionary<string, DbIrtPeptide>(value); }
        }

        private IDictionary<string, DbIrtPeptide> DictLibrary
        {
            get { return _dictLibrary; }
            set { _dictLibrary = new ImmutableDictionary<string, DbIrtPeptide>(value);}
        }

        private ISession OpenWriteSession()
        {
            return new SessionWithLock(_sessionFactory.OpenSession(), _databaseLock, true);
        }

        public IEnumerable<DbIrtPeptide> StandardPeptides
        {
            get { return DictStandards.Values; }
        }

        public int StandardPeptideCount
        {
            get { return DictStandards.Count; }
        }

        public IEnumerable<DbIrtPeptide> LibraryPeptides
        {
            get { return DictLibrary.Values; }
        }

        public int LibraryPeptideCount
        {
            get { return DictLibrary.Count; }
        }

        public DbIrtPeptide GetPeptide(string seq)
        {
            DbIrtPeptide pep;
            if (DictStandards.TryGetValue(seq, out pep) || DictLibrary.TryGetValue(seq, out pep))
                return pep;
            return null;
        }

        #region Property change methods

        public IrtDb AddPeptides(IEnumerable<DbIrtPeptide> peptides)
        {
            var dictLibrary = new Dictionary<string, DbIrtPeptide>(_dictLibrary);
            AddPeptides(peptides, dictLibrary);

            return ChangeProp(ImClone(this), im => im.DictLibrary = dictLibrary);

        }

        public IrtDb UpdateStandard(IEnumerable<DbIrtPeptide> standard)
        {
            if (standard.Any(peptide => !peptide.Standard))
                throw new InvalidOperationException("Attempt to update standard with non-standard peptide.");

            var dictStandard = standard.ToDictionary(peptide => peptide.PeptideModSeq);

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                foreach (var peptide in StandardPeptides)
                    session.Delete(peptide);
                transaction.Commit();
            }

            AddPeptides(standard, null);

            return ChangeProp(ImClone(this), im => im.DictStandards = dictStandard);
        }

        private void AddPeptides(IEnumerable<DbIrtPeptide> peptides, IDictionary<string, DbIrtPeptide> dictLibrary)
        {
            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                foreach (var peptide in peptides)
                    session.SaveOrUpdate(peptide);
                transaction.Commit();
            }

            foreach (var peptide in peptides.Where(peptide => !peptide.Standard))
            {
                dictLibrary.Remove(peptide.PeptideModSeq);
                dictLibrary.Add(peptide.PeptideModSeq, peptide);
            }
        }

        public IrtDb DeletePeptides(IEnumerable<string> seqs)
        {
            IDictionary<string, DbIrtPeptide> dictStandards = _dictStandards;
            IDictionary<string, DbIrtPeptide> dictLibrary = _dictLibrary;

            using (var session = OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                foreach (var seq in seqs)
                    DeletePeptide(seq, session, ref dictStandards, ref dictLibrary);
                transaction.Commit();
            }

            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     if (!ReferenceEquals(dictStandards, _dictStandards))
                                                         DictStandards = dictStandards;
                                                     if (!ReferenceEquals(dictLibrary, _dictLibrary))
                                                         DictLibrary = dictLibrary;
                                                 });
        }

        private void DeletePeptide(string seq, ISession session,
            ref IDictionary<string, DbIrtPeptide> dictStandards,
            ref IDictionary<string, DbIrtPeptide> dictLibrary)
        {
            DbIrtPeptide pep;
            IDictionary<string, DbIrtPeptide> dict;
            if (dictStandards.TryGetValue(seq, out pep))
                dict = CloneLibraryToChange(_dictStandards, ref dictStandards);
            else if (_dictLibrary.TryGetValue(seq, out pep))
                dict = CloneLibraryToChange(_dictLibrary, ref dictLibrary);
            else
                return;

            session.Delete(pep);

            dict.Remove(seq);
        }

        private static IDictionary<string, DbIrtPeptide> CloneLibraryToChange(
                    IDictionary<string, DbIrtPeptide> dictOrig,
                    ref IDictionary<string, DbIrtPeptide> dictNew)
        {
            if (ReferenceEquals(dictNew, dictOrig))
                dictNew = new Dictionary<string, DbIrtPeptide>(dictOrig);
            return dictNew;
        }

        #endregion

        public static IrtDb CreateIrtDb(string path)
        {
            using (SessionFactoryFactory.CreateSessionFactory(path, true))
            {
            }
            var irtDb = new IrtDb(path).Load(null, null);
            using (var session = irtDb.OpenWriteSession())
            using (var transaction = session.BeginTransaction())
            {
                session.Save(new DbVersionInfo {SchemaVersion = SCHEMA_VERSION_CURRENT});
                transaction.Commit();
            }
            return irtDb;
        }

        //Throws DatabaseOpeningException
        public static IrtDb GetIrtDb(string path, IProgressMonitor loadMonitor)
        {
            var status = new ProgressStatus(string.Format("Loading iRT database {0}", path));
            if (loadMonitor != null)
                loadMonitor.UpdateProgress(status);

            try
            {
                if (path == null)
                    throw new DatabaseOpeningException("Database path cannot be null");

                if (!File.Exists(path))
                    throw new DatabaseOpeningException(String.Format("The file {0} does not exist.", path));

                string message;
                try
                {
                    //Check for a valid SQLite file and that it has our schema
                    return new IrtDb(path).Load(loadMonitor, status);
                }
                catch (UnauthorizedAccessException)
                {
                    message = String.Format("You do not have privileges to access the file {0}", path);
                }
                catch (DirectoryNotFoundException)
                {
                    message = String.Format("The path containing {0} does not exist", path);
                }
                catch (FileNotFoundException)
                {
                    message =
                        String.Format("The file {0} could not be created. Perhaps you do not have sufficient privileges.",
                                      path);
                }
                catch (SQLiteException)
                {
                    message = String.Format("The file {0} is not a valid iRT database file.", path);
                }
                catch (Exception)
                {
                    message = String.Format("The file {0} could not be opened.", path);
                }

                throw new DatabaseOpeningException(message);
            }
            catch (DatabaseOpeningException x)
            {
                if (loadMonitor == null)
                    throw;
                loadMonitor.UpdateProgress(status.ChangeErrorException(x));
                return null;
            }
        }
    }
}
