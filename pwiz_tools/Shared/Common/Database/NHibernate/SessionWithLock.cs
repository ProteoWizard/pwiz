/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Engine;
using NHibernate.Stat;
using NHibernate.Type;

#pragma warning disable 612,618
namespace pwiz.Common.Database.NHibernate
{
    public class SessionWithLock : AbstractSessionWithLock, ISession
    {
        readonly ISession _session;

        public SessionWithLock(ISession session, ReaderWriterLock readerWriterLock, bool writeLock)
            : this(session, readerWriterLock, writeLock, CancellationToken.None)
        {
            
        }

        public SessionWithLock(ISession session, ReaderWriterLock readerWriterLock, bool writeLock, CancellationToken cancellationToken)
            :base(readerWriterLock, writeLock, cancellationToken, session.CancelQuery)
        {
            _session = session;
        }

        public override void Dispose()
        {
            _session.Dispose();
            base.Dispose();
        }

        public void Flush()
        {
            _session.Flush();
        }

        public DbConnection Disconnect()
        {
            return _session.Disconnect();
        }

        public void Reconnect()
        {
            _session.Reconnect();
        }

        public void Reconnect(DbConnection connection)
        {
            _session.Reconnect(connection);
        }

        public DbConnection Close()
        {
            return _session.Close();
        }

        public void CancelQuery()
        {
            _session.CancelQuery();
        }

        public bool IsDirty()
        {
            return _session.IsDirty();
        }

        public object GetIdentifier(object obj)
        {
            return _session.GetIdentifier(obj);
        }

        public bool Contains(object obj)
        {
            return _session.Contains(obj);
        }

        public void Evict(object obj)
        {
            _session.Evict(obj);
        }

        public object Load(Type theType, object id, LockMode lockMode)
        {
            return _session.Load(theType, id, lockMode);
        }

        public object Load(Type theType, object id)
        {
            return _session.Load(theType, id);
        }

        public T Load<T>(object id, LockMode lockMode)
        {
            return _session.Load<T>(id, lockMode);
        }

        public T Load<T>(object id)
        {
            return _session.Load<T>(id);
        }

        public void Load(object obj, object id)
        {
            _session.Load(obj, id);
        }

        public void Replicate(object obj, ReplicationMode replicationMode)
        {
            _session.Replicate(obj, replicationMode);
        }

        public void Replicate(string entityName, object obj, ReplicationMode replicationMode)
        {
            _session.Replicate(entityName, obj, replicationMode);
        }

        public object Save(object obj)
        {
            EnsureWriteLock();
            return _session.Save(obj);
        }

        public void Save(object obj, object id)
        {
            EnsureWriteLock();
            _session.Save(obj, id);
        }

        public object Save(string entityName, object obj)
        {
            EnsureWriteLock();
            return _session.Save(entityName, obj);
        }

        public void SaveOrUpdate(object obj)
        {
            EnsureWriteLock();
            _session.SaveOrUpdate(obj);
        }

        public void SaveOrUpdate(string entityName, object obj)
        {
            EnsureWriteLock();
            _session.SaveOrUpdate(entityName, obj);
        }

        public void Update(object obj)
        {
            EnsureWriteLock();
            _session.Update(obj);
        }

        public void Update(object obj, object id)
        {
            EnsureWriteLock();
            _session.Update(obj, id);
        }

        public void Update(string entityName, object obj)
        {
            EnsureWriteLock();
            _session.Update(entityName, obj);
        }

        public object Merge(object obj)
        {
            EnsureWriteLock();
            return _session.Merge(obj);
        }

        public object Merge(string entityName, object obj)
        {
            EnsureWriteLock();
            return _session.Merge(entityName, obj);
        }

        public void Persist(object obj)
        {
            EnsureWriteLock();
            _session.Persist(obj);
        }

        public void Persist(string entityName, object obj)
        {
            EnsureWriteLock();
            _session.Persist(entityName, obj);
        }

        public void Delete(object obj)
        {
            EnsureWriteLock();
            _session.Delete(obj);
        }

        public int Delete(string query)
        {
            EnsureWriteLock();
            return _session.Delete(query);
        }

        public int Delete(string query, object value, IType type)
        {
            EnsureWriteLock();
            return _session.Delete(query, value, type);
        }

        public int Delete(string query, object[] values, IType[] types)
        {
            EnsureWriteLock();
            return _session.Delete(query, values, types);
        }

        public void Lock(object obj, LockMode lockMode)
        {
            _session.Lock(obj, lockMode);
        }

        public void Lock(string entityName, object obj, LockMode lockMode)
        {
            _session.Lock(entityName, obj, lockMode);
        }

        public void Refresh(object obj)
        {
            _session.Refresh(obj);
        }

        public void Refresh(object obj, LockMode lockMode)
        {
            _session.Refresh(obj, lockMode);
        }

        public LockMode GetCurrentLockMode(object obj)
        {
            return _session.GetCurrentLockMode(obj);
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

        public ICriteria CreateCriteria(Type persistentClass)
        {
            return _session.CreateCriteria(persistentClass);
        }

        public ICriteria CreateCriteria(Type persistentClass, string alias)
        {
            return _session.CreateCriteria(persistentClass, alias);
        }

        public IQuery CreateQuery(string queryString)
        {
            return _session.CreateQuery(queryString);
        }

        public IQuery CreateFilter(object collection, string queryString)
        {
            return _session.CreateFilter(collection, queryString);
        }

        public IQuery GetNamedQuery(string queryName)
        {
            return _session.GetNamedQuery(queryName);
        }

        public ISQLQuery CreateSQLQuery(string queryString)
        {
            return _session.CreateSQLQuery(queryString);
        }

        public void Clear()
        {
            _session.Clear();
        }

        public object Get(Type clazz, object id)
        {
            return _session.Get(clazz, id);
        }

        public object Get(Type clazz, object id, LockMode lockMode)
        {
            return _session.Get(clazz, id, lockMode);
        }

        public object Get(string entityName, object id)
        {
            return _session.Get(entityName, id);
        }

        public T Get<T>(object id)
        {
            return _session.Get<T>(id);
        }

        public T Get<T>(object id, LockMode lockMode)
        {
            return _session.Get<T>(id, lockMode);
        }

        public string GetEntityName(object obj)
        {
            return _session.GetEntityName(obj);
        }

        public IFilter EnableFilter(string filterName)
        {
            return _session.EnableFilter(filterName);
        }

        public IFilter GetEnabledFilter(string filterName)
        {
            return _session.GetEnabledFilter(filterName);
        }

        public void DisableFilter(string filterName)
        {
            _session.DisableFilter(filterName);
        }

        public IMultiQuery CreateMultiQuery()
        {
            return _session.CreateMultiQuery();
        }

        public ISession SetBatchSize(int batchSize)
        {
            return _session.SetBatchSize(batchSize);
        }

        public ISessionImplementor GetSessionImplementation()
        {
            return _session.GetSessionImplementation();
        }

        public IMultiCriteria CreateMultiCriteria()
        {
            return _session.CreateMultiCriteria();
        }

        public ISession GetSession(EntityMode entityMode)
        {
            return _session.GetSession(entityMode);
        }

        public FlushMode FlushMode
        {
            get { return _session.FlushMode; }
            set { _session.FlushMode = value; }
        }

        public CacheMode CacheMode
        {
            get { return _session.CacheMode; }
            set { _session.CacheMode = value; }
        }

        public ISessionFactory SessionFactory
        {
            get { return _session.SessionFactory; }
        }

        public DbConnection Connection
        {
            get { return _session.Connection; }
        }

        public bool IsOpen
        {
            get { return _session.IsOpen; }
        }

        public bool IsConnected
        {
            get { return _session.IsConnected; }
        }

        public ITransaction Transaction
        {
            get { return _session.Transaction; }
        }

        public ISessionStatistics Statistics
        {
            get { return _session.Statistics; }
        }

        public object Load(string entityName, object id, LockMode lockMode)
        {
            return _session.Load(entityName, id, lockMode);
        }

        public object Load(string entityName, object id)
        {
            return _session.Load(entityName, id);
        }

        public void Delete(string entityName, object obj)
        {
            _session.Delete(entityName, obj);
        }

        public ICriteria CreateCriteria<T>() where T : class
        {
            return _session.CreateCriteria<T>();
        }

        public ICriteria CreateCriteria<T>(string alias) where T : class
        {
            return _session.CreateCriteria<T>(alias);
        }

        public ICriteria CreateCriteria(string entityName)
        {
            return _session.CreateCriteria(entityName);
        }

        public ICriteria CreateCriteria(string entityName, string alias)
        {
            return _session.CreateCriteria(entityName, alias);
        }

        public bool IsReadOnly(object entityOrProxy)
        {
            return _session.IsReadOnly(entityOrProxy);
        }

        public void SetReadOnly(object entityOrProxy, bool readOnly)
        {
            _session.SetReadOnly(entityOrProxy, readOnly);
        }

        public T Merge<T>(T entity) where T : class
        {
            EnsureWriteLock();
            return _session.Merge(entity);
        }

        public T Merge<T>(string entityName, T entity) where T : class
        {
            EnsureWriteLock();
            return _session.Merge(entityName, entity);
        }

        public IQueryOver<T, T> QueryOver<T>() where T : class
        {
            return _session.QueryOver<T>();
        }

        public IQueryOver<T, T> QueryOver<T>(Expression<Func<T>> alias) where T : class
        {
            return _session.QueryOver(alias);
        }

        public IQueryOver<T, T> QueryOver<T>(string entityName) where T : class
        {
            return _session.QueryOver<T>(entityName);
        }

        public IQueryOver<T, T> QueryOver<T>(string entityName, Expression<Func<T>> alias) where T : class
        {
            return _session.QueryOver(entityName, alias);
        }

        public bool DefaultReadOnly
        {
            get { return _session.DefaultReadOnly; }
            set { _session.DefaultReadOnly = value; }
        }

        public void Save(string entityName, object obj, object id)
        {
            EnsureWriteLock();
            _session.Save(entityName, obj, id);
        }

        public void SaveOrUpdate(string entityName, object obj, object id)
        {
            EnsureWriteLock();
            _session.SaveOrUpdate(entityName, obj, id);
        }

        public void Update(string entityName, object obj, object id)
        {
            EnsureWriteLock();
            _session.Update(entityName, obj, id);
        }

        public Task FlushAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.FlushAsync(cancellationToken);
        }

        public Task<bool> IsDirtyAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.IsDirtyAsync(cancellationToken);
        }

        public Task EvictAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.EvictAsync(obj, cancellationToken);
        }

        public Task<object> LoadAsync(Type theType, object id, LockMode lockMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync(theType, id, lockMode, cancellationToken);
        }

        public Task<object> LoadAsync(string entityName, object id, LockMode lockMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync(entityName, id, lockMode, cancellationToken);
        }

        public Task<object> LoadAsync(Type theType, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync(theType, id, cancellationToken);
        }

        public Task<T> LoadAsync<T>(object id, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync<T>(id, lockMode, cancellationToken);
        }

        public Task<T> LoadAsync<T>(object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync<T>(id, cancellationToken);
        }

        public Task<object> LoadAsync(string entityName, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync(entityName, id, cancellationToken);
        }

        public Task LoadAsync(object obj, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LoadAsync(obj, id, cancellationToken);
        }

        public Task ReplicateAsync(object obj, ReplicationMode replicationMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.ReplicateAsync(obj, replicationMode, cancellationToken);
        }

        public Task ReplicateAsync(string entityName, object obj, ReplicationMode replicationMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.ReplicateAsync(entityName, obj, replicationMode, cancellationToken);
        }

        public Task<object> SaveAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveAsync(obj, cancellationToken);
        }

        public Task SaveAsync(object obj, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveAsync(obj, id, cancellationToken);
        }

        public Task<object> SaveAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveAsync(entityName, obj, cancellationToken);
        }

        public Task SaveAsync(string entityName, object obj, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveAsync(entityName, obj, id, cancellationToken);
        }

        public Task SaveOrUpdateAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveOrUpdateAsync(obj, cancellationToken);
        }

        public Task SaveOrUpdateAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveOrUpdateAsync(entityName, obj, cancellationToken);
        }

        public Task SaveOrUpdateAsync(string entityName, object obj, object id,
            CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.SaveOrUpdateAsync(entityName, obj, id, cancellationToken);
        }

        public Task UpdateAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(obj, cancellationToken);
        }

        public Task UpdateAsync(object obj, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(obj, id, cancellationToken);
        }

        public Task UpdateAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(entityName, obj, cancellationToken);
        }

        public Task UpdateAsync(string entityName, object obj, object id,
            CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.UpdateAsync(entityName, obj, id, cancellationToken);
        }

        public Task<object> MergeAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.MergeAsync(obj, cancellationToken);
        }

        public Task<object> MergeAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.MergeAsync(entityName, obj, cancellationToken);
        }

        public Task<T> MergeAsync<T>(T entity, CancellationToken cancellationToken = new CancellationToken()) where T : class
        {
            EnsureWriteLock();
            return _session.MergeAsync(entity, cancellationToken);
        }

        public Task<T> MergeAsync<T>(string entityName, T entity, CancellationToken cancellationToken = new CancellationToken()) where T : class
        {
            EnsureWriteLock();
            return _session.MergeAsync(entityName, entity, cancellationToken);
        }

        public Task PersistAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.PersistAsync(obj, cancellationToken);
        }

        public Task PersistAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.PersistAsync(entityName, obj, cancellationToken);
        }

        public Task DeleteAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(obj, cancellationToken);
        }

        public Task DeleteAsync(string entityName, object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(entityName, obj, cancellationToken);
        }

        public Task<int> DeleteAsync(string query, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(query, cancellationToken);
        }

        public Task<int> DeleteAsync(string query, object value, IType type, CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(query, value, type, cancellationToken);
        }

        public Task<int> DeleteAsync(string query, object[] values, IType[] types,
            CancellationToken cancellationToken = new CancellationToken())
        {
            EnsureWriteLock();
            return _session.DeleteAsync(query, values, types, cancellationToken);
        }

        public Task LockAsync(object obj, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LockAsync(obj, lockMode, cancellationToken);
        }

        public Task LockAsync(string entityName, object obj, LockMode lockMode,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.LockAsync(entityName, obj, lockMode, cancellationToken);
        }

        public Task RefreshAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(obj, cancellationToken);
        }

        public Task RefreshAsync(object obj, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.RefreshAsync(obj, lockMode, cancellationToken);
        }

        public Task<IQuery> CreateFilterAsync(object collection, string queryString,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.CreateFilterAsync(collection, queryString, cancellationToken);
        }

        public Task<object> GetAsync(Type clazz, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync(clazz, id, cancellationToken);
        }

        public Task<object> GetAsync(Type clazz, object id, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync(clazz, id, lockMode, cancellationToken);
        }

        public Task<object> GetAsync(string entityName, object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync(entityName, id, cancellationToken);
        }

        public Task<T> GetAsync<T>(object id, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync<T>(id, cancellationToken);
        }

        public Task<T> GetAsync<T>(object id, LockMode lockMode, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetAsync<T>(id, lockMode, cancellationToken);
        }

        public Task<string> GetEntityNameAsync(object obj, CancellationToken cancellationToken = new CancellationToken())
        {
            return _session.GetEntityNameAsync(obj, cancellationToken);
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

        public ISharedSessionBuilder SessionWithOptions()
        {
            return _session.SessionWithOptions();
        }
    }
}
