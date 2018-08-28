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
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Engine;

namespace pwiz.Common.Database.NHibernate
{
    public class StatelessSessionWithLock : AbstractSessionWithLock, IStatelessSession
    {
        private readonly IStatelessSession _session;


        public StatelessSessionWithLock(IStatelessSession session, ReaderWriterLock readerWriterLock, bool writeLock, CancellationToken cancellationToken)
            :base(readerWriterLock, writeLock, cancellationToken, ()=>CancelQuery(session))
        {
            _session = session;
        }

        private static void CancelQuery(IStatelessSession session)
        {
            try
            {
                ISessionImplementor sessionImplementor = session.GetSessionImplementation();
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

        public DbConnection Connection
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

        public Task<object> InsertAsync(object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.InsertAsync(entity, cancellationToken);
        }

        public Task<object> InsertAsync(string entityName, object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.InsertAsync(entityName, entity, cancellationToken);
        }

        public Task UpdateAsync(object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(entity, cancellationToken);
        }

        public Task UpdateAsync(string entityName, object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(entityName, entity, cancellationToken);
        }

        public Task DeleteAsync(object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(entity, cancellationToken);
        }

        public Task DeleteAsync(string entityName, object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(entityName, entity, cancellationToken);
        }

        public Task<object> GetAsync(string entityName, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync(entityName, id, cancellationToken);
        }

        public Task<T> GetAsync<T>(object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync<T>(id, cancellationToken);
        }

        public Task<object> GetAsync(string entityName, object id, LockMode lockMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync(entityName, id, lockMode, cancellationToken);
        }

        public Task<T> GetAsync<T>(object id, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync<T>(id, lockMode, cancellationToken);
        }

        public Task RefreshAsync(object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(entity, cancellationToken);
        }

        public Task RefreshAsync(string entityName, object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(entityName, entity, cancellationToken);
        }

        public Task RefreshAsync(object entity, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(entity, lockMode, cancellationToken);
        }

        public Task RefreshAsync(string entityName, object entity, LockMode lockMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(entityName, entity, lockMode, cancellationToken);
        }

        public void JoinTransaction()
        {
            _session.JoinTransaction();
        }

        public IQueryable<T> Query<T>()
        {
            return _session.Query<T>();
        }

        public IQueryable<T> Query<T>(string entityName)
        {
            return _session.Query<T>(entityName);
        }
    }
}
