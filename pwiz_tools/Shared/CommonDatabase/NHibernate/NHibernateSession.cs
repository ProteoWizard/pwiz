using System;
using System.Data;
using NHibernate;

namespace CommonDatabase.NHibernate
{
    public abstract class NHibernateSession : IDisposable
    {
        public static Stateful OfSession(NHibernateSessionFactory sessionFactory, ISession session)
        {
            return new Stateful(sessionFactory, session)
            {
                LeaveOpen = true,
            };
        }

        protected NHibernateSession(NHibernateSessionFactory sessionFactory, DbConnection dbConnection)
        {
            SessionFactory = sessionFactory;
            DbConnection = dbConnection;
        }

        public NHibernateSessionFactory SessionFactory { get; }

        public bool LeaveOpen { get; protected set; }
        public DbConnection DbConnection { get; private set; }
        public IDbConnection Connection
        {
            get { return DbConnection.Connection; }
        }
        public abstract ICriteria CreateCriteria(Type persistentClass);

        public virtual void Dispose()
        {
        }

        public class Stateful : NHibernateSession
        {
            public Stateful(NHibernateSessionFactory sessionFactory, ISession session) : base(sessionFactory, DbConnection.Of(session.Connection))
            {
                Session = session;
            }

            public ISession Session { get; }

            public override ICriteria CreateCriteria(Type persistentClass)
            {
                return Session.CreateCriteria(persistentClass);
            }

            public override void Dispose()
            {
                if (!LeaveOpen)
                {
                    Session.Dispose();
                }
                base.Dispose();
            }
        }

        public class Stateless : NHibernateSession
        {
            public Stateless(NHibernateSessionFactory sessionFactory, IStatelessSession statelessSession) : base(
                sessionFactory, DbConnection.Of(statelessSession.Connection))
            {
                StatelessSession = statelessSession;
            }

            public IStatelessSession StatelessSession { get; }
            public override ICriteria CreateCriteria(Type persistentClass)
            {
                return StatelessSession.CreateCriteria(persistentClass);
            }

            public override void Dispose()
            {
                StatelessSession.Dispose();
                base.Dispose();
            }
        }
    }
}
