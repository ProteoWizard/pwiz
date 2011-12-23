using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.ui.Forms
{
    public class BasePeptideAnalysesForm : WorkspaceForm
    {
        private HashSet<long> _analysisIdsToRequery;
        private ISession _session;
        private bool _inRequery;
        
        public BasePeptideAnalysesForm(Workspace workspace) : base(workspace)
        {
            
        }
        private BasePeptideAnalysesForm() : this(null)
        {
        }

        private void Requery()
        {
            ICollection<long> peptideAnalysisIdsToRequery;
            lock (this)
            {
                if (_inRequery)
                {
                    return;
                }
                _inRequery = true;
                peptideAnalysisIdsToRequery = _analysisIdsToRequery;
                _analysisIdsToRequery = new HashSet<long>();
            }
            try
            {
                using (_session = Workspace.OpenSession())
                {
                    Requery(_session, peptideAnalysisIdsToRequery);
                }
            }
            catch (Exception e)
            {
                ErrorHandler.LogException(Name, "Exception querying data", e);
            }
            finally
            {
                lock(this)
                {
                    _session = null;
                    _inRequery = false;
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            new Action(Requery).BeginInvoke(null, null);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            lock(this)
            {
                if (_inRequery)
                {
                    if (_session != null)
                    {
                        try
                        {
                            _session.CancelQuery();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            var changedPeptideAnalysisIds = new HashSet<long>(args.GetChangedPeptideAnalyses().Keys);
            foreach (var peptideFileAnalysis in args.GetEntities<PeptideFileAnalysis>())
            {
                changedPeptideAnalysisIds.Add(peptideFileAnalysis.PeptideAnalysis.Id.Value);
            }
            AddAnalysisIdsToRequery(changedPeptideAnalysisIds);
        }

        protected virtual void Requery(ISession session, ICollection<long> peptideAnalysisIdsToRequery)
        {
        }

        protected void AddAnalysisIdsToRequery(IEnumerable<long> ids)
        {
            lock(this)
            {
                if (_analysisIdsToRequery == null)
                {
                    return;
                }
                _analysisIdsToRequery.UnionWith(ids);
            }
        }
    }
}
