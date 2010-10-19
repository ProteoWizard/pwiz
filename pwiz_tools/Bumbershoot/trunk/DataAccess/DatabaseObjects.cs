using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DataMapping;
using NHibernate;
using NHibernate.Criterion;
using System.IO;

namespace DataAccess
{
    public class DatabaseObjects
    {
        private ISession _session;

        public DatabaseObjects(ISession recievedSession)
        {
            _session = recievedSession;
        }

        public IList<HistoryItem> GetHistoryItemList()
        {
            return _session.CreateCriteria(typeof(HistoryItem))
                .AddOrder(Order.Asc("RowNumber"))
                .List<HistoryItem>();
        }

        public IList<ConfigFile> GetConfigFileList()
        {
            return _session.CreateCriteria(typeof(ConfigFile))
                .List<ConfigFile>();
        }

        public HistoryItem GetSpecificHistoryItemByID(int jobIndex)
        {
            return _session.Get<HistoryItem>(jobIndex);
        }

        public IList<ConfigFile> retrieveConfigFilesByFilePath(string filePath)
        {
            return _session.CreateCriteria(typeof(ConfigFile))
                .Add(Expression.Eq("FilePath",filePath))
                .AddOrder(Order.Desc("FirstUsedDate"))
                .List<ConfigFile>();
        }

        public void SaveItem(object obj)
        {
            _session.SaveOrUpdate(obj);
            _session.Flush();
        }

        public void DeleteItem(object obj)
        {
            _session.Delete(obj);
            _session.Flush();
        }

        public void IndicateJobBegin(int jobIndex)
        {
            HistoryItem hi = _session.Get<HistoryItem>(jobIndex);
            hi.StartTime = DateTime.Now;
            _session.Update(hi);
            _session.Flush();
        }

        public void IndicateJobEnd(int jobIndex, bool unsuccessful)
        {
            HistoryItem hi = _session.Get<HistoryItem>(jobIndex);
            if (unsuccessful)
                hi.CurrentStatus = "Unsuccessful";
            else
                hi.CurrentStatus = "Finished";
            hi.EndTime = DateTime.Now;
            _session.Update(hi);
            _session.Flush();
        }

        public void UpdateStatus(int jobIndex, string newStatus)
        {
            HistoryItem hi = _session.Get<HistoryItem>(jobIndex);
            hi.CurrentStatus = newStatus;
            _session.Update(hi);
            _session.Flush();
        }

        public void SaveRowNumber(int jobIndex, int rowNumber)
        {
            HistoryItem hi = _session.Get<HistoryItem>(jobIndex);
            hi.RowNumber = rowNumber;
            _session.Update(hi);
            _session.Flush();
        }
    }
}
