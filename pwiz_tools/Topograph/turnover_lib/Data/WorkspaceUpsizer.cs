using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Data
{
    public class WorkspaceUpsizer : ILongOperationJob
    {
        private const int RowBatchSize = 1000;
        private ISessionFactory _sourceSessionFactory;
        private ISessionFactory _targetSessionFactory;
        private ISession _sourceSession;
        private IStatelessSession _targetSession;
        private LongOperationBroker _longOperationBroker;
        public WorkspaceUpsizer(ISessionFactory sourceSessionFactory, ISessionFactory targetSessionFactory)
        {
            _sourceSessionFactory = sourceSessionFactory;
            _targetSessionFactory = targetSessionFactory;
        }

        private HashSet<long> GetExistingIds<T>() where T : IDbEntity
        {
            var list = new List<long>();
            _targetSession.CreateQuery("SELECT T.Id FROM " + typeof (T) + " T").List(list);
            return new HashSet<long>(list);
        }

        private void CopyEntities<T>() where T : IDbEntity
        {
            var existing = GetExistingIds<T>();
            int count = 0;
            var query = _sourceSession.CreateQuery("FROM " + typeof (T));
            _longOperationBroker.UpdateStatusMessage("Copying " + typeof(T).Name);
            _targetSession.BeginTransaction();
            foreach (T entity in query.Enumerable())
            {
                if (existing.Contains(entity.Id.Value))
                {
                    continue;
                }
                count++;
                _longOperationBroker.UpdateStatusMessage("Copying " + typeof(T).Name + " #" + count);
                _targetSession.Insert(entity);
                if (count % RowBatchSize == 0)
                {
                    _targetSession.Transaction.Commit();
                    _targetSession.BeginTransaction();
                }
            }
            _targetSession.Transaction.Commit();
        }

        private void CopyChromatograms()
        {
            var existing = GetExistingIds<DbChromatogram>();
            int count = 0;
            var query = _sourceSession.CreateQuery("FROM " + typeof (DbChromatogram));
            _longOperationBroker.UpdateStatusMessage("Copying chromatograms");
            _targetSession.BeginTransaction();
            foreach (DbChromatogram chromatogram in query.Enumerable())
            {
                if (existing.Contains(chromatogram.Id.Value))
                {
                    continue;
                }
                count++;
                _longOperationBroker.UpdateStatusMessage("Copying chromatogram #" + count);
                if (chromatogram.UncompressedSize == null)
                {
                    chromatogram.ChromatogramPoints = chromatogram.ChromatogramPoints;
                }
                _targetSession.Insert(chromatogram);
                if (count % RowBatchSize == 0)
                {
                    _targetSession.Transaction.Commit();
                    _targetSession.BeginTransaction();
                }
            }
            _targetSession.Transaction.Commit();
        }
        
        public void Run(LongOperationBroker longOperationBroker)
        {
            _longOperationBroker = longOperationBroker;
            using (_sourceSession = _sourceSessionFactory.OpenSession())
            {
                using (_targetSession = _targetSessionFactory.OpenStatelessSession())
                {
                    CopyEntities<DbWorkspace>();
                    CopyEntities<DbMsDataFile>();
                    CopyEntities<DbSetting>();
                    CopyEntities<DbModification>();
                    CopyEntities<DbTracerDef>();
                    CopyEntities<DbPeptide>();
                    CopyEntities<DbPeptideSearchResult>();
                    CopyEntities<DbPeptideAnalysis>();
                    CopyEntities<DbPeptideFileAnalysis>();
                    CopyChromatograms();
                    CopyEntities<DbPeak>();
                    CopyEntities<DbPeptideDistribution>();
                    CopyEntities<DbPeptideAmount>();
                }
            }
        }

        public bool Cancel()
        {
            if (_sourceSession != null)
            {
                _sourceSession.CancelQuery();
            }
            return true;
        }
    }
}
