/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Data;
using System.Linq.Expressions;
using System.Threading;
using NHibernate;
using NHibernate.Engine;

namespace pwiz.Common.Database.NHibernate
{
    public class StatelessSessionWithLock : AbstractSessionWithLock, IStatelessSession
    {
        private readonly IStatelessSession _session;


        public StatelessSessionWithLock(IStatelessSession session, ReaderWriterLock readerWriterLock, bool writeLock, CancellationToken cancellationToken)
            :base(readerWriterLock, writeLock, cancellationToken)
        {
            _session = session;
        }

        public override void CancelQuery()
        {
            try
            {
                ISessionImplementor sessionImplementor = _session.GetSessionImplementation();
                if (sessionImplementor != null && sessionImplementor.Batcher != null)
                {
                    sessionImplementor.Batcher.CancelLastQuery();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public object Insert(object entity)
        {
            EnsureWriteLock();
            return _session.Insert(entity);
        }

        public object Insert(string entityName, object entity)
        {
            EnsureWriteLock();
            return _session.Insert(entityName, entity);
        }

        public void Update(object entity)
        {
            EnsureWriteLock();
            _session.Update(entity);
        }

        public void Update(string entityName, object entity)
        {
            EnsureWriteLock();
            _session.Update(entityName, entity);
        }

        public void Delete(object entity)
        {
            EnsureWriteLock();
            _session.Delete(entity);
        }

        public void Delete(string entityName, object entity)
        {
            EnsureWriteLock();
            _session.Delete(entityName, entity);
        }

        public object Get(string entityName, object id)
        {
            return _session.Get(entityName, id);
        }

        public T Get<T>(object id)
        {
            return _session.Get<T>(id);
        }

        public object Get(string entityName, object id, LockMode lockMode)
        {
            return _session.Get(entityName, id, lockMode);
        }

        public T Get<T>(object id, LockMode lockMode)
        {
            return _session.Get<T>(id, lockMode);
        }

        public void Refresh(object entity)
        {
            _session.Refresh(entity);
        }

        public void Refresh(string entityName, object entity)
        {
            _session.Refresh(entityName, entity);
        }

        public void Refresh(object entity, LockMode lockMode)
        {
            _session.Refresh(entity, lockMode);
        }

        public void Refresh(string entityName, object entity, LockMode lockMode)
        {
            _session.Refresh(entityName, entity, lockMode);
        }

        public IQuery CreateQuery(string queryString)
        {
            return _session.CreateQuery(queryString);
        }

        public IQuery GetNamedQuery(string queryName)
        {
            return _session.GetNamedQuery(queryName);
        }

        public ICriteria CreateCriteria<T>() where T : class
        {
            return _session.CreateCriteria<T>();
        }

        public ICriteria CreateCriteria<T>(string alias) where T : class
        {
            return _session.CreateCriteria<T>(alias);
        }

        public ICriteria CreateCriteria(Type entityType)
        {
            return _session.CreateCriteria(entityType);
        }

        public ICriteria CreateCriteria(Type entityType, string alias)
        {
            return _session.CreateCriteria(entityType, alias);
        }

        public ICriteria CreateCriteria(string entityName)
        {
            return _session.CreateCriteria(entityName);
        }

        public ICriteria CreateCriteria(string entityName, string alias)
        {
            return _session.CreateCriteria(entityName, alias);
        }

        public IQueryOver<T, T> QueryOver<T>() where T : class
        {
            return _session.QueryOver<T>();
        }

        public IQueryOver<T, T> QueryOver<T>(Expression<Func<T>> alias) where T : class
        {
            return _session.QueryOver(alias);
        }

        public ISQLQuery CreateSQLQuery(string queryString)
        {
            return _session.CreateSQLQuery(queryString);
        }

        public ITransaction BeginTransaction()
        {
            EnsureWriteLock();
            return _session.BeginTransaction();
        }

        public ITransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            EnsureWriteLock();
            return _session.BeginTransaction(isolationLevel);
        }

        public IStatelessSession SetBatchSize(int batchSize)
        {
            return _session.SetBatchSize(batchSize);
        }

        public IDbConnection Connection
        {
            get { return _session.Connection; }
        }

        public ITransaction Transaction
        {
            get { return _session.Transaction; }
        }

        public bool IsOpen
        {
            get { return _session.IsOpen; }
        }

        public bool IsConnected
        {
            get { return _session.IsConnected; }
        }

        public void Close()
        {
            _session.Close();
        }

        public ISessionImplementor GetSessionImplementation()
        {
            return _session.GetSessionImplementation();
        }

        public override void Dispose()
        {
            _session.Dispose();
            base.Dispose();
        }
    }
}
