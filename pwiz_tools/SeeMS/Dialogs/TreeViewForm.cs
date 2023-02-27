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
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace seems
{
    // courtesy of https://stackoverflow.com/a/10052686/638445
    public class NoHScrollTree : TreeView
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x8000; // TVS_NOHSCROLL
                return cp;
            }
        }
    }

    public partial class TreeViewForm : Form
    {
        public TreeView TreeView { get { return treeView; } }

        public TreeViewForm( GraphItem item = null )
        {
            InitializeComponent();
        }

        private void updateNodeBounds( TreeNode node, bool expandedOnly, ref Size bounds )
        {
            bounds.Height += node.TreeView.ItemHeight;
            bounds.Width = Math.Max( node.Bounds.Right,
                                     bounds.Width );
            if( !expandedOnly || node.IsExpanded )
                foreach( TreeNode childNode in node.Nodes )
                    updateNodeBounds( childNode, expandedOnly, ref bounds );
        }

        public Size GetNodeBounds(bool expandedOnly)
        {
            if( treeView.Nodes.Count == 0 )
                return new Size();
            Size bounds = new Size();
            foreach( TreeNode rootNode in treeView.Nodes )
                updateNodeBounds( rootNode, expandedOnly, ref bounds );
            return bounds;
        }

        public void DoAutoSize()
        {
            //Application.DoEvents();
            Size nodeSize = GetNodeBounds( true );
            //nodeSize = GetNodeBounds( true );
            nodeSize.Height += 3;
            nodeSize.Width += 3;
            treeView.Size = nodeSize;
            nodeSize.Height += 6;
            nodeSize.Width += 6;
            this.Size = nodeSize;
            //MessageBox.Show( treeView.Size.ToString() + "\r\n" + Size.ToString() );
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }

        }
    }
}