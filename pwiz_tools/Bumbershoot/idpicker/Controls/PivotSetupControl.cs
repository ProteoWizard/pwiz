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

namespace IDPicker.Controls
{
    /// <summary>
    /// Allows viewing and manipulation of a table's pivoted columns.
    /// </summary>
    /// <typeparam name="T">A client-supplied type to facilitate distinguishing Pivot instances.</typeparam>
    public partial class PivotSetupControl<T> : UserControl
    {
        public PivotSetupControl (IEnumerable<Pivot<T>> pivots)
        {
            InitializeComponent();

            DoubleBuffered = true;
            ResizeRedraw = true;

            if(pivots == null || pivots.Count() == 0)
                throw new ArgumentException("no pivots");

            objectListView.ShowSortIndicators = false;
            objectListView.CustomSorter = new SortDelegate((x,y) => { }); // noop

            objectListView.ItemChecked += new ItemCheckedEventHandler(objectListView_ItemChecked);

            fieldNameColumn.AspectGetter += delegate(object x) { return (x as Pivot<T>).Text; };
            objectListView.CheckedAspectName = "Checked";

            objectListView.SetObjects(pivots);
            Height = objectListView.RowHeightEffective * (objectListView.GetItemCount() + 3);
        }

        protected override void WndProc (ref Message m)
        {
            if (Parent is PopupControl.Popup && (Parent as PopupControl.Popup).ProcessResizing(ref m))
                return;
            base.WndProc(ref m);
        }

        void objectListView_ItemChecked (object sender, ItemCheckedEventArgs e)
        {
            if (PivotChanged != null)
                PivotChanged(this, new PivotChangedEventArgs<T>((e.Item as OLVListItem).RowObject as Pivot<T>));
        }

        /// <summary>
        /// Occurs when the user changes the checked state of a pivot.
        /// </summary>
        public event EventHandler<PivotChangedEventArgs<T>>  PivotChanged;

        /// <summary>
        /// Checks or unchecks the specified pivot mode.
        /// </summary>
        public void SetPivot(T pivotMode, bool checked_)
        {
            var pivot = Pivots.First(o => o.Mode.Equals(pivotMode));
            if (checked_) objectListView.CheckObject(pivot);
            else objectListView.UncheckObject(pivot);
        }

        /// <summary>
        /// Returns a read-only list of all the available pivots.
        /// </summary>
        public ReadOnlyCollection<Pivot<T>> Pivots
        {
            get
            {
                return new ReadOnlyCollection<Pivot<T>>(objectListView.Objects.Cast<Pivot<T>>().ToList());
            }
        }

        /// <summary>
        /// Returns a read-only list of the checked (enabled) pivots.
        /// </summary>
        public ReadOnlyCollection<Pivot<T>> CheckedPivots
        {
            get
            {
                return new ReadOnlyCollection<Pivot<T>>(objectListView.CheckedObjects.Cast<Pivot<T>>().ToList());
            }
        }
    }

    /// <summary>
    /// Represents one row in a PivotSetupControl.
    /// </summary>
    /// <typeparam name="T">A client-supplied type to facilitate distinguishing Pivot instances.</typeparam>
    public class Pivot<T>
    {
        public Pivot () { Checked = false; }
        public Pivot (bool checked_) { Checked = checked_; }

        /// <summary>
        /// An instance of the client-supplied type (useful for distinguishing this Pivot instance).
        /// </summary>
        public T Mode { get; set; }

        /// <summary>
        /// If true, the host table should be pivoted on this Pivot.
        /// </summary>
        public bool Checked { get; private set; }

        /// <summary>
        /// A string describing this Pivot.
        /// </summary>
        public string Text { get; set; }
    }

    public class PivotChangedEventArgs<T> : EventArgs
    {
        public PivotChangedEventArgs (Pivot<T> pivot) { Pivot = pivot; }
        public Pivot<T> Pivot { get; private set; }
    }
}
