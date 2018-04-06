//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using ZedGraph;

namespace seems
{
    public partial class DataPointTableForm : DockableForm, IDataView
    {
        private GraphItem item;
        private IPointList pointList;

        public DataPointTableForm(GraphItem graphItem)
        {
            InitializeComponent();

            item = graphItem;
            pointList = item.Points;
            dataGridView.Rows.Insert( 0, pointList.Count );
        }

        private void dataGridView_CellValueNeeded( object sender, DataGridViewCellValueEventArgs e )
        {
            if( pointList == null  )
                throw new InvalidOperationException( "cell value needed but point list is null" );

            if( e.ColumnIndex == 0 )
                e.Value = pointList[e.RowIndex].X;
            else
                e.Value = pointList[e.RowIndex].Y;
        }

        /// <summary>
        /// updates the list of data points and then refreshes the form
        /// </summary>
        public override void Refresh()
        {
            pointList = item.Points;
            base.Refresh();
        }

        #region IDataView Members

        public IList<ManagedDataSource> Sources
        {
            get { return new List<ManagedDataSource>() { item.Source }; }
        }

        public IList<GraphItem> DataItems
        {
            get { return new List<GraphItem>() { item }; }
        }

        #endregion
    }
}
