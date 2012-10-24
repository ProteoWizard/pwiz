using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class QueryExecuter : ILongOperationJob
    {
        public QueryExecuter(ISession session, IQuery query, IList results)
        {
            Session = session;
            Query = query;
            Results = results;
        }

        public ISession Session { get; private set; }
        public IQuery Query { get; private set; }
        public IList Results { get; private set; }

        public void Run(LongOperationBroker longOperationBroker)
        {
            Query.List(Results);
        }

        public bool Cancel()
        {
            Session.CancelQuery();
            return true;
        }
    }
}
