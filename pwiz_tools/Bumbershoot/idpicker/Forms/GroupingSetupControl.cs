//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BrightIdeasSoftware;
using PopupControl;

namespace IDPicker.Forms
{
    public partial class GroupingSetupControl<T> : UserControl
    {
        public GroupingSetupControl (IEnumerable<Grouping<T>> groupings)
        {
            InitializeComponent();

            DoubleBuffered = true;
            ResizeRedraw = true;

            var rearrangingDropSink = new RearrangingDropSink(false);
            objectListView.DropSink = rearrangingDropSink;

            objectListView.ShowSortIndicators = false;
            objectListView.CustomSorter = new SortDelegate((x,y) => { }); // noop

            objectListView.ItemChecked += new ItemCheckedEventHandler(objectListView_ItemChecked);
            rearrangingDropSink.Dropped += new EventHandler<OlvDropEventArgs>(rearrangingDropSink_Dropped);
            rearrangingDropSink.CanDrop +=new EventHandler<OlvDropEventArgs>(rearrangingDropSink_CanDrop);

            fieldNameColumn.AspectGetter += delegate(object x) { return (x as Grouping<T>).Text; };
            objectListView.CheckedAspectName = "Checked";

            objectListView.SetObjects(groupings);
        }

        void rearrangingDropSink_CanDrop(object sender, OlvDropEventArgs e)
        {
            if (GroupingChanging != null)
            {
                var eventArgs = new GroupingChangingEventArgs<T>()
                                    {
                                        Grouping = (e.DataObject as OLVDataObject).ModelObjects[0] as Grouping<T>,
                                        NewIndex = Math.Max(0, e.DropTargetIndex )
                                    };
                if (e.DropTargetItem != null && ReferenceEquals(eventArgs.Grouping, e.DropTargetItem.RowObject))
                    e.Effect = DragDropEffects.None;
                else
                {
                    GroupingChanging(this, eventArgs);
                    if (eventArgs.Cancel)
                        e.Effect = DragDropEffects.None;
                }
            }
        }

        protected override void WndProc (ref Message m)
        {
            if ((Parent as Popup).ProcessResizing(ref m))
            {
                return;
            }
            base.WndProc(ref m);
        }

        void rearrangingDropSink_Dropped (object sender, OlvDropEventArgs e)
        {
            if (GroupingChanged != null)
                GroupingChanged(this, EventArgs.Empty);
        }

        void objectListView_ItemChecked (object sender, ItemCheckedEventArgs e)
        {
            if (GroupingChanged != null)
                GroupingChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Occurs as the user reorders or changes the checked state of the groupings.
        /// </summary>
        public event EventHandler<GroupingChangingEventArgs<T>> GroupingChanging;

        /// <summary>
        /// Occurs when the user reorders or changes the checked state of the groupings.
        /// </summary>
        public event EventHandler GroupingChanged;

        /// <summary>
        /// Returns a read-only list of all the available groupings.
        /// </summary>
        public ReadOnlyCollection<Grouping<T>> Groupings
        {
            get
            {
                return new ReadOnlyCollection<Grouping<T>>(objectListView.Objects.Cast<Grouping<T>>().ToList());
            }
        }

        /// <summary>
        /// Returns a read-only list of the checked (enabled) groupings.
        /// </summary>
        public ReadOnlyCollection<Grouping<T>> CheckedGroupings
        {
            get
            {
                return new ReadOnlyCollection<Grouping<T>>(objectListView.CheckedObjects.Cast<Grouping<T>>().ToList());
            }
        }

        /// <summary>
        /// Given a grouping mode, finds the next grouping in CheckedGroupings.
        /// </summary>
        public Grouping<T> GetChildGrouping (T parentMode)
        {
            return GetChildGrouping(CheckedGroupings, parentMode);
        }

        /// <summary>
        /// Given a grouping mode, finds the next grouping in CheckedGroupings.
        /// </summary>
        public static Grouping<T> GetChildGrouping (IList<Grouping<T>> checkedGroupings, T parentMode)
        {
            var parent = checkedGroupings.Where(o => o.Mode.Equals(parentMode)).SingleOrDefault();
            if (parent == null)
                throw new ArgumentException("Invalid parent mode.");

            int parentIndex = checkedGroupings.IndexOf(parent);
            if (parentIndex == checkedGroupings.Count - 1)
                return null;

            return checkedGroupings[parentIndex + 1];
        }

        /// <summary>
        /// Given parent and child grouping modes, returns true iff parent==child or parent is before child in groupings.
        /// </summary>
        public static bool HasParentGrouping (IList<Grouping<T>> groupings, T childMode, T parentMode)
        {
            if (parentMode.Equals(childMode))
                return true;

            var child = groupings.Where(o => o.Mode.Equals(childMode)).SingleOrDefault();
            if (child == null)
                throw new ArgumentException("Invalid child mode.");

            var parent = groupings.Where(o => o.Mode.Equals(parentMode)).SingleOrDefault();
            if (parent == null)
                return false;

            return groupings.IndexOf(parent) < groupings.IndexOf(child);
        }
    }

    public class Grouping<T>
    {
        public Grouping () { Checked = false; }
        public Grouping (bool checked_) { Checked = checked_; }

        public T Mode { get; set; }
        public bool Checked { get; private set; }
        public string Text { get; set; }
    }

    public class GroupingChangingEventArgs<T> : CancelEventArgs
    {
        public Grouping<T> Grouping { get; set; }
        public int NewIndex { get; set; }
    }
}
