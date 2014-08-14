/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Windows.Forms;

// Fix incorrect handling of double click in TreeView.
namespace SkylineTester
{
    public partial class MyTreeView : TreeView
    {
        private readonly List<TreeNode> _foundNodes = new List<TreeNode>(); 

        protected override void WndProc(ref Message m)
        {
            // Filter WM_LBUTTONDBLCLK
            if (m.Msg != 0x203) base.WndProc(ref m);
        }

        public int Find(string text, int position)
        {
            Focus();
            _foundNodes.Clear();
            Find(text.ToLower(), Nodes[0]);
            if (position >= _foundNodes.Count)
                return -1;
            SelectedNode = _foundNodes[position];
            return position + 1;
        }

        private void Find(string text, TreeNode startNode)
        {
            while (startNode != null)
            {
                if (startNode.Text.ToLower().Contains(text))
                    _foundNodes.Add(startNode);
                if (startNode.Nodes.Count != 0)
                    Find(text, startNode.Nodes[0]); 
                startNode = startNode.NextNode;
            }
        }
    }
}
