/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Diagnostics;
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
                        catch(Exception exception)
                        {
                            Trace.TraceWarning("Exception cancelling query:{0}", exception);
                        }
                    }
                }
            }
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
