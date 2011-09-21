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
using System.IO;
using System.Threading;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Criterion;
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

        private ISessionFactory SessionFactory { get; set; }
        public ReaderWriterLock DatabaseLock { get; private set; }
        public String FilePath { get; private set; }

        private IrtDb(String path)
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

        public void ConfigureMappings(Configuration configuration)
        {
            SessionFactoryFactory.ConfigureMappings(configuration);
        }

        public static IrtDb OpenIrtDb(String path)
        {
            return new IrtDb(path);
        }

        public static IrtDb CreateIrtDb(String path)
        {
            using (SessionFactoryFactory.CreateSessionFactory(path, true))
            {
            }
            return OpenIrtDb(path);
        }

        public void AddPeptides(IEnumerable<DbIrtPeptide> peptides)
        {
            using (var session = OpenWriteSession())
            {
                session.BeginTransaction();
                foreach (var peptide in peptides)
                {
                    session.SaveOrUpdate(peptide);
                }
                session.Transaction.Commit();
            }
        }

        public void UpdateStandard(IEnumerable<DbIrtPeptide> standard)
        {
            using (var session = OpenWriteSession())
            {
                session.BeginTransaction();
                var peptides = new List<DbIrtPeptide>();
                session.CreateCriteria(typeof (DbIrtPeptide))
                    .Add(Restrictions.Eq("Standard", true))
                    .List(peptides);
                
                foreach (var peptide in peptides)
                {
                    session.Delete(peptide);
                }
                session.Transaction.Commit();
            }

            AddPeptides(standard);
        }

        public List<DbIrtPeptide> GetStandard()
        {
            using (var session = OpenSession())
            {
                var standard = new List<DbIrtPeptide>();
                session.CreateCriteria(typeof(DbIrtPeptide))
                     .Add(Restrictions.Eq("Standard", true)).List(standard);

                return standard;
            }
        }

        public List<DbIrtPeptide> GetLibrary()
        {
            using (var session = OpenSession())
            {
                var standard = new List<DbIrtPeptide>();
                session.CreateCriteria(typeof(DbIrtPeptide))
                     .Add(Restrictions.Eq("Standard", false)).List(standard);

                return standard;
            }
        }

        public DbIrtPeptide GetPeptide(string seq)
        {
            using (var session = OpenSession())
            {
                var peps =
                    session.CreateCriteria(typeof (DbIrtPeptide)).Add(Restrictions.Eq("PeptideModSeq", seq)).List<DbIrtPeptide>();
                if(peps.Count < 1)
                    return null;
                return peps[0];
            }
        }

        public List<DbIrtPeptide> GetPeptides(List<string> seqs)
        {
            List<DbIrtPeptide> peps = new List<DbIrtPeptide>();
            using (var session = OpenSession())
            {
                foreach (var pep in seqs)
                {
                    //This will throw if the peptide is in the DB more than once
                    var pep2 = session.CreateCriteria(typeof (DbIrtPeptide)).Add(Restrictions.Eq("PeptideModSeq", pep)).
                        Add(Restrictions.Eq("Standard", false)).
                        UniqueResult<DbIrtPeptide>();

                    peps.Add(pep2); //Add a null entry if need be
                }
            }

            return peps;
        }

        public void DeletePeptide(string seq)
        {
            using (var session = OpenWriteSession())
            {
                session.BeginTransaction();

                var peps =
                    session.CreateCriteria(typeof (DbIrtPeptide)).Add(Restrictions.Eq("PeptideModSeq", seq)).List
                        <DbIrtPeptide>();
                if (peps.Count < 1)
                    return;

                if (peps[0] != null)
                    session.Delete(peps[0]);
                
                session.Transaction.Commit();
            }
        }

        //Throws DatabaseOpeningException
        public static IrtDb GetIrtDb(string path)
        {
            if (path == null)
                throw new DatabaseOpeningException("Database path cannot be null");

            string message;

            if (!File.Exists(path))
                throw new DatabaseOpeningException(String.Format("The file {0} does not exist.", path));

            try
            {
                //Check for a valid SQLite file and that it has our schema
                var db = OpenIrtDb(path);
                db.GetStandard();

                return db;
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
    }
}
