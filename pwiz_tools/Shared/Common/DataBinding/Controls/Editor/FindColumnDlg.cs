/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// Dialog for searching for columns by name in the ViewEditor.
    /// </summary>
    public partial class FindColumnDlg : CommonFormEx
    {
        private Searcher _searcher;
        private Form _owner;

        public FindColumnDlg()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _owner = Owner;
            if (null != _owner)
            {
                _owner.FormClosed += OwnerFormClosed;
            }
            tbxFind.Focus(); // Presumably the user invoked the dialog with intent to search, save them a click
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (null != _owner)
            {
                _owner.FormClosed -= OwnerFormClosed;
            }
            Dispose();
        }

        private void OwnerFormClosed(object sender, EventArgs args)
        {
            Close();
        }

        public ViewEditor ViewEditor { get; set; }

        public AvailableFieldsTree AvailableFieldsTree
        {
            get
            {
                if (null == ViewEditor)
                {
                    return null;
                }
                return ViewEditor.ActiveAvailableFieldsTree;
            }
        }

        private void GoToSearchResult(PropertyPath propertyPath)
        {
            if (null == _searcher || !Equals(_searcher.SearchState, GetSearchState()))
            {
                return;
            }
            if (null == propertyPath)
            {
                return;
            }
            AvailableFieldsTree.SelectColumn(propertyPath);
        }

        private void FindNextButtonOnClick(object sender, EventArgs eventArgs)
        {
            if (null != _searcher)
            {
                GoToSearchResult(_searcher.FindNextResult);
            }
        }

        private void FindPreviousButtonOnClick(object sender, EventArgs eventArgs)
        {
            if (null != _searcher)
            {
                GoToSearchResult(_searcher.FindPreviousResult);
            }
        }

        /// <summary>
        /// Does a depth-first search through all of the columns in the AvailableFieldsTree
        /// for columns matching the criteria. Because of recursion, the set of available columns might
        /// be never-ending, so this class stops looking after <see cref="MAX_COLUMNS_TO_SEARCH"/>.
        /// </summary>
        private class Searcher
        {
            private const int MAX_COLUMNS_TO_SEARCH = 1000000;
            private Queue<ColumnDescriptor> _queue;
            private ColumnDescriptor _previous;
            private ColumnDescriptor _next;
            private bool _pastSelectedColumn;
            private int _totalCounter;
            public Searcher(FindColumnDlg columnFinder, SearchState searchState)
            {
                ColumnFinder = columnFinder;
                SearchState = searchState;
                _queue = new Queue<ColumnDescriptor>();
                _queue.Enqueue(searchState.RootColumn);
            }

            public FindColumnDlg ColumnFinder { get; private set; }
            public SearchState SearchState { get; private set; }

            public void DoWork()
            {
                if (_pastSelectedColumn && null != _next)
                {
                    return;
                }
                DateTime start = DateTime.Now;
                int counter = 0;
                while (0 < _queue.Count)
                {
                    if (counter > 1000 && DateTime.Now.Subtract(start) > TimeSpan.FromMilliseconds(50))
                    {
                        return;
                    }
                    if (_totalCounter > MAX_COLUMNS_TO_SEARCH)
                    {
                        return;
                    }
                    string normalizedFindText = SearchState.MatchCase
                        ? SearchState.FindText
                        : SearchState.FindText.ToLower();
                    var column = _queue.Dequeue();
                    counter++;
                    _totalCounter++;
                    bool isSelected = Equals(column.PropertyPath, SearchState.SelectedPath);
                    string columnCaption = column.GetColumnCaption(ColumnCaptionType.localized);
                    if (!SearchState.MatchCase)
                    {
                        columnCaption = columnCaption.ToLower();
                    }
                    bool matches = columnCaption.Contains(normalizedFindText);
                    if (isSelected)
                    {
                        _pastSelectedColumn = true;
                    }
                    if (matches)
                    {
                        if (isSelected)
                        {
                            // don't find selected column
                        }
                        else if (_pastSelectedColumn)
                        {
                            _next = column;
                            return;
                        }
                        else
                        {
                            _previous = column;
                        }
                    }
                    foreach (var child in SearchState.AvailableFieldsTree.ListChildren(column))
                    {
                        _queue.Enqueue(child);
                    }
                }
            }

            public bool KnowsPrevious { get { return _pastSelectedColumn; } }
            public bool KnowsNext { get { return null != _next || _queue.Count == 0; }}

            public PropertyPath FindNextResult
            {
                get { return _next == null ? null : _next.PropertyPath; }
            }
            public PropertyPath FindPreviousResult { get { return _previous == null ? null : _previous.PropertyPath; } }
        }

        private struct SearchState
        {
            public AvailableFieldsTree AvailableFieldsTree;
            public ColumnDescriptor RootColumn;
            public PropertyPath SelectedPath;
            public String FindText;
            public Boolean MatchCase;
        }

        private SearchState GetSearchState()
        {
            if (null == AvailableFieldsTree)
            {
                return default(SearchState);
            }
            PropertyPath selectedPath = PropertyPath.Root;
            if (null != AvailableFieldsTree.SelectedNode)
            {
                selectedPath = AvailableFieldsTree.GetTreeColumn(AvailableFieldsTree.SelectedNode).PropertyPath;
            }
            return new SearchState
            {
                AvailableFieldsTree = AvailableFieldsTree,
                RootColumn = AvailableFieldsTree.RootColumn,
                FindText = tbxFind.Text,
                MatchCase = cbCaseSensitive.Checked,
                SelectedPath = selectedPath,
            };
        }
        private void timer_Tick(object sender, EventArgs e)
        {
            var searchState = GetSearchState();
            if (string.IsNullOrEmpty(searchState.FindText))
            {
                _searcher = null;
            }
            else
            {
                if (_searcher != null && !Equals(_searcher.SearchState, searchState))
                {
                    _searcher = null;
                }
                if (null == _searcher)
                {
                    _searcher = new Searcher(this, searchState);
                }
                _searcher.DoWork();
            }
            UpdateButtons();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void UpdateButtons()
        {
            if (_searcher == null)
            {
                SetButtonVisualEnabled(btnFindNext, false);
                SetButtonVisualEnabled(btnFindPrevious, false);
                return;
            }
            if (_searcher.KnowsPrevious)
            {
                SetButtonVisualEnabled(btnFindPrevious, null != _searcher.FindPreviousResult);
            }
            else
            {
                SetButtonVisualEnabled(btnFindPrevious, null);
            }
            if (_searcher.KnowsNext)
            {
                SetButtonVisualEnabled(btnFindNext, null != _searcher.FindNextResult);
            }
            else
            {
                SetButtonVisualEnabled(btnFindNext, null);
            }
        }

        private void SetButtonVisualEnabled(Button button, bool? enabled)
        {
            if (enabled.HasValue)
            {
                button.Cursor = Cursors.Default;
            }
            else
            {
                button.Cursor = Cursors.WaitCursor;
            }
        }

        #region Test Methods

        public string FindText
        {
            get { return tbxFind.Text; }
            set { tbxFind.Text = value; }
        }

        public void SearchForward()
        {
            GoToSearchResult(_searcher.FindNextResult);
        }

        public void SearchBackward()
        {
            GoToSearchResult(_searcher.FindPreviousResult);
        }

        public bool IsReadyToSearch()
        {
            if (string.IsNullOrEmpty(tbxFind.Text))
            {
                return true;
            }
            return _searcher != null && Equals(_searcher.SearchState, GetSearchState())
                && _searcher.KnowsNext && _searcher.KnowsPrevious;
        }
        #endregion
    }
}
