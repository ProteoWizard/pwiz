using System;
using NHibernate;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.MsData
{
    public class TaskItem : IDisposable
    {
        public TaskItem(Workspace workspace, DbLock dbLock)
        {
            Workspace = workspace;
            DbLock = dbLock;
        }
        public bool CanSave()
        {
// ReSharper disable ConditionIsAlwaysTrueOrFalse
            return DbLock != null && DbLock.Id != null;
// ReSharper restore ConditionIsAlwaysTrueOrFalse
        }
        public void BeginLock()
        {
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
            if (Workspace.SessionFactory == null)
            {
                return;
            }
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
