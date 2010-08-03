using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using NHibernate.Cfg;

namespace DataAccess
{
    public class SessionManager
    {
        private ISessionFactory sessionFactory;

        public SessionManager()
        {
            sessionFactory = GetSessionFactory();
        }

        public ISession GetSession()
        {
            return sessionFactory.OpenSession();
        }

        private ISessionFactory GetSessionFactory()
        {
            return (new Configuration()).Configure().BuildSessionFactory();
        }
    }
}