/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparisonModel
    {
        private readonly object _lock = new object();
        private readonly IDocumentContainer _documentContainer;
        private GroupComparisonDef _groupComparisonDef = GroupComparisonDef.EMPTY;
        private GroupComparer _groupComparer;
        private CancellationTokenSource _cancellationTokenSource;
        private SrmDocument _document;
        private GroupComparisonResults _results;
        private int _percentComplete;
        private readonly HashSet<EventHandler> _modelChangedListeners = new HashSet<EventHandler>();
        private readonly QrFactorizationCache _qrFactorizationCache = new QrFactorizationCache();

        public GroupComparisonModel(IDocumentContainer documentContainer, string groupComparisonName)
        {
            _documentContainer = documentContainer;
            GroupComparisonName = groupComparisonName;
            Document = documentContainer.Document;
        }

        public GroupComparisonDef GroupComparisonDef
        {
            get
            {
                lock (_lock)
                {
                    return _groupComparisonDef;
                }
            }
            set
            {
                // TODO(tobiasr): ask nick why this might be a problem
//                if (!string.IsNullOrEmpty(GroupComparisonName))
//                {
//                    throw new InvalidOperationException();
//                }
                lock (_lock)
                {
                    if (Equals(GroupComparisonDef, value))
                    {
                        return;
                    }
                    _groupComparisonDef = value;
                    RestartCalculation();
                }
                FireModelChanged();
            }
        }

        public SrmDocument ApplyChangesToDocument(SrmDocument doc, GroupComparisonDef groupDef)
        {
            var groupComparisons = doc.Settings.DataSettings.GroupComparisonDefs.ToList();
            int index =
                groupComparisons.FindIndex(def => def.Name == GroupComparisonName);
            if (index < 0)
            {
                groupComparisons.Add(groupDef);
            }
            else
            {
                groupComparisons[index] = groupDef;
            }
            doc =
                doc.ChangeSettings(
                    doc.Settings.ChangeDataSettings(
                        doc.Settings.DataSettings.ChangeGroupComparisonDefs(groupComparisons)));
            return doc;
        }

        public string GroupComparisonName { get; private set; }

        public GroupComparer GroupComparer
        {
            get
            {
                lock (_lock)
                {
                    return _groupComparer;
                }
            }
        }

        public IDocumentContainer DocumentContainer
        {
            get { return _documentContainer; }
        }

        public int PercentComplete
        {
            get
            {
                lock (this)
                {
                    return _percentComplete;
                }
            }
        }

        private void DocumentContainerOnChange(object sender, DocumentChangedEventArgs args)
        {
            Document = _documentContainer.Document;
        }

        public SrmDocument Document
        {
            get
            {
                lock (_lock)
                {
                    return _document;
                }
            }
            private set
            {
                lock (_lock)
                {
                    if (Equals(Document, value))
                    {
                        return;
                    }
                    _document = value;
                    if (!string.IsNullOrEmpty(GroupComparisonName))
                    {
                        _groupComparisonDef = Document.Settings.DataSettings.GroupComparisonDefs.FirstOrDefault(
                            def => def.Name == GroupComparisonName)
                                             ?? GroupComparisonDef.EMPTY;
                    }
                    RestartCalculation();
                }
            }
        }

        public event EventHandler ModelChanged
        {
            add
            {
                lock (_lock)
                {
                    if (_modelChangedListeners.Count == 0)
                    {
                        _documentContainer.Listen(DocumentContainerOnChange);
                        Document = _documentContainer.Document;
                    }
                    if (!_modelChangedListeners.Add(value))
                    {
                        throw new ArgumentException("Listener already added"); // Not L10N
                    }
                    RestartCalculation();
                }
            }
            remove
            {
                lock (_lock)
                {
                    if (!_modelChangedListeners.Remove(value))
                    {
                        throw new ArgumentException("Listener not added"); // Not L10N
                    }
                    if (_modelChangedListeners.Count == 0)
                    {
                        _documentContainer.Unlisten(DocumentContainerOnChange);
                        if (_cancellationTokenSource != null)
                        {
                            _cancellationTokenSource.Cancel();
                        }
                    }
                }
            }
        }

        public IDisposable AddModelChanged(Control control, Action<GroupComparisonModel> eventHandler)
        {
            return new ModelChangeSupport(this, control, eventHandler);
        }

        public GroupComparisonResults Results
        {
            get
            {
                lock (_lock)
                {
                    return _results;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (ReferenceEquals(Results, value))
                    {
                        return;
                    }
                    _results = value;
                }
                FireModelChanged();
            }
        }

        private void RestartCalculation()
        {
            var srmDocument = Document;
            var groupComparisonDef = GroupComparisonDef;
            if (_results != null)
            {
                if (Equals(_results.Document, srmDocument) && Equals(_results.GroupComparer.ComparisonDef, groupComparisonDef))
                {
                    return;
                }
            }
            if (null != _cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
            }
            if (0 == _modelChangedListeners.Count)
            {
                return;
            }
            if (_groupComparer == null || !Equals(srmDocument, _groupComparer.SrmDocument) ||
                !Equals(groupComparisonDef, _groupComparer.ComparisonDef))
            {
                _groupComparer = new GroupComparer(groupComparisonDef, srmDocument, _qrFactorizationCache);
            }
            _cancellationTokenSource = new CancellationTokenSource();
            if (null != GroupComparisonDef && null != Document)
            {
                _percentComplete = 0;
                GroupComparer groupComparer = _groupComparer;
                var cancellationToken = _cancellationTokenSource.Token;
                RunAsync(() =>
                    {
                        var results = ComputeComparisonResults(groupComparer, srmDocument, cancellationToken);
                        lock (_lock)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                Results = results;
                                _percentComplete = 100;
                            }
                        }
                    });
            }
        }

        private void RunAsync(Action action)
        {
            ActionUtil.RunAsync(action, "Group Comparison");    // Not L10N
        }

        private void FireModelChanged()
        {
            EventHandler[] eventHandlers;
            lock (_lock)
            {
                eventHandlers = _modelChangedListeners.ToArray();
            }
            var eventArgs = new EventArgs();
            foreach (var eventHandler in eventHandlers)
            {
                eventHandler(this, eventArgs);
            }
        }

        private GroupComparisonResults ComputeComparisonResults(GroupComparer groupComparer, SrmDocument document,
            CancellationToken cancellationToken)
        {
            DateTime startTime = DateTime.Now;

            List<GroupComparisonResult> results = new List<GroupComparisonResult>();
            if (groupComparer.IsValid)
            {
                var peptideGroups = document.MoleculeGroups.ToArray();
                for (int i = 0; i < peptideGroups.Length; i++)
                {
                    int percentComplete = 100 * i / peptideGroups.Length;
                    lock (_lock)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _percentComplete = percentComplete;
                    }
                    var peptideGroup = peptideGroups[i];
                    IEnumerable<PeptideDocNode> peptides;
                    if (groupComparer.ComparisonDef.PerProtein)
                    {
                        peptides = new PeptideDocNode[] {null};
                    }
                    else
                    {
                        peptides = peptideGroup.Molecules;
                    }
                    foreach (var peptide in peptides)
                    {
                        results.AddRange(groupComparer.CalculateFoldChanges(peptideGroup, peptide));
                    }
                }
            }
            DateTime endTime = DateTime.Now;
            return new GroupComparisonResults(groupComparer, results, startTime, endTime);
        }

        private class ModelChangeSupport : IDisposable
        {
            private readonly GroupComparisonModel _model;
            private Control _control;
            private readonly Action<GroupComparisonModel> _eventHandler;
            private bool _controlHandleCreated;

            public ModelChangeSupport(GroupComparisonModel model, Control control, Action<GroupComparisonModel> eventHandler)
            {
                _model = model;
                _control = control;
                _eventHandler = eventHandler;
                _control.HandleCreated += ControlHandleCreated;
                _control.HandleDestroyed += ControlHandleDestroyed;
            }

            private void ControlHandleCreated(object sender, EventArgs args)
            {
                _model.ModelChanged += ModelOnModelChanged;
                _controlHandleCreated = true;
                _eventHandler(_model);
            }

            private void ControlHandleDestroyed(object sender, EventArgs args)
            {
                _controlHandleCreated = false;
                _model.ModelChanged -= ModelOnModelChanged;
            }

            private void ModelOnModelChanged(object sender, EventArgs args)
            {
                if (_controlHandleCreated)
                {
                    try
                    {
                        _control.BeginInvoke(new Action(() =>
                        {
                            _eventHandler(_model);
                        }));
                    }
                    catch (Exception exception)
                    {
                        if (_controlHandleCreated)
                        {
                            Program.ReportException(exception);
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (null != _control)
                {
                    _control.HandleCreated -= ControlHandleCreated;
                    _control.HandleDestroyed -= ControlHandleDestroyed;
                    if (_controlHandleCreated)
                    {
                        ControlHandleDestroyed(_control, new EventArgs());
                    }
                    _control = null;
                }
            }
        }
    }
}
