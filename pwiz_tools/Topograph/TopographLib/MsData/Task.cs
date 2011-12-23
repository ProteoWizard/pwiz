using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public class Task : IDisposable
    {
        public Task(Workspace workspace, DbLock dbLock)
        {
            Workspace = workspace;
            DbLock = dbLock;
        }
        public WorkspaceVersion WorkspaceVersion { get; private set; }
        public bool CanSave()
        {
            return DbLock != null && DbLock.Id != null;
        }
        public void BeginLock()
        {
            using (Workspace.GetReadLock())
            {
                WorkspaceVersion = Workspace.WorkspaceVersion;
                if (DbLock == null)
                {
                    return;
                }
                using (var session = Workspace.OpenWriteSession())
                {
                    try
                    {
                        session.BeginTransaction();
                        session.Save(DbLock);
                        session.Transaction.Commit();
                    }
                    catch (HibernateException hibernateException)
                    {
                        throw new LockException("Could not insert lock", hibernateException);
                    }
                }
            }
        }
        public void UpdateLock(ISession session)
        {
            var dbLock = session.Get<DbLock>(DbLock.Id);
            session.Update(dbLock);
        }
        public void FinishLock(ISession session)
        {
            var dbLock = session.Get<DbLock>(DbLock.Id);
            session.Delete(dbLock);
        }
        public Workspace Workspace { get; private set; }
        public DbLock DbLock { get; private set; }
        public void Dispose()
        {
            if (DbLock != null && DbLock.Id != null)
            {
                try
                {
                    using (var session = Workspace.OpenWriteSession())
                    {
                        var dbLock = session.Get<DbLock>(DbLock.Id);
                        if (dbLock != null)
                        {
                            session.BeginTransaction();
                            session.Delete(dbLock);
                            session.Transaction.Commit();
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.Out.WriteLine(exception);
                }
                finally
                {
                    DbLock = null;
                }
            }
        }
    }
    public class LockException : ApplicationException
    {
        public LockException(string message, Exception cause) : base(message, cause)
        {
        }
    }
}
