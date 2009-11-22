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

        private void CopyEntities<T>() where T : IDbEntity
        {
            int count = 0;
            var query = _sourceSession.CreateQuery("FROM " + typeof (T));
            _longOperationBroker.UpdateStatusMessage("Copying " + typeof(T).Name);
            foreach (T entity in query.Enumerable())
            {
                count++;
                _longOperationBroker.UpdateStatusMessage("Copying " + typeof(T).Name + " #" + count);
                _targetSession.Insert(entity);
            }
        }

        private void CopyChromatograms()
        {
            int count = 0;
            var query = _sourceSession.CreateQuery("FROM " + typeof (DbChromatogram));
            _longOperationBroker.UpdateStatusMessage("Copying chromatograms");
            foreach (DbChromatogram chromatogram in query.Enumerable())
            {
                count++;
                _longOperationBroker.UpdateStatusMessage("Copying chromatogram #" + count);
                if (chromatogram.UncompressedSize == null)
                {
                    chromatogram.ChromatogramPoints = chromatogram.ChromatogramPoints;
                }
                _targetSession.Insert(chromatogram);
            }
        }
        
        public void Run(LongOperationBroker longOperationBroker)
        {
            _longOperationBroker = longOperationBroker;
            using (_sourceSession = _sourceSessionFactory.OpenSession())
            {
                using (_targetSession = _targetSessionFactory.OpenStatelessSession())
                {
                    _targetSession.BeginTransaction();
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
                    longOperationBroker.UpdateStatusMessage("Committing transaction");
                    _targetSession.Transaction.Commit();
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
