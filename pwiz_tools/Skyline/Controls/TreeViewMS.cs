using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls
{
	/// <summary>
    /// A MultiSelect TreeView. See http://www.codeproject.com/KB/tree/treeviewms.aspx for details.
	/// </summary>
    public class TreeViewMS : TreeView
    {
       

        protected ArrayList MColl;
        protected TreeNode MLastNode, AnchorNode;

        public TreeViewMS()
        {
            MColl = new ArrayList();

        }

        protected IDocumentUIContainer DocumentContainer
        { 
            get;
            set;
        }


	    [Browsable (false)]
        public ArrayList SelectedNodes
        {
            get
            {
                return MColl;
            }
            set
            {
                MColl.Clear();
                MColl = value;
                
            }
        }


       

        // Triggers
        //
        // (overriden method, and base class called to ensure events are triggered)

	   protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
        {
            base.OnBeforeSelect(e);

            bool bControl = (ModifierKeys == Keys.Control);
	        bool bShift = (ModifierKeys == Keys.Shift);

            
            // selecting twice the node while pressing CTRL ?
            if (bControl && MColl.Contains(e.Node) && !DocumentContainer.InUndoRedo)
            {
                // unselect it (let framework know we don't want selection this time)
                e.Cancel = true;

                // update nodes
                MColl.Remove(e.Node);
                return;
            }

            
            if (!bShift) MLastNode = AnchorNode = e.Node; // store begin of shift sequence
        }


        protected override void OnAfterSelect(TreeViewEventArgs e)
        {
            base.OnAfterSelect(e);

            bool bControl = (ModifierKeys == Keys.Control);
            bool bShift = (ModifierKeys == Keys.Shift);
            

            if (bControl && !DocumentContainer.InUndoRedo)
            {
                if (!MColl.Contains(e.Node)) // new node ?
                {
                    MColl.Add(e.Node);
                }
                else  // not new, remove it from the collection
                {
                    
                    MColl.Remove(e.Node);
                }
                
            }
            else
            {
                // SHIFT is pressed
                if (bShift)
                {
                    TreeNode uppernode = MLastNode;
                    TreeNode bottomnode = e.Node;
                    
                    if(!(bottomnode.Index < uppernode.Index && uppernode.Index < AnchorNode.Index) 
                        && !(bottomnode.Index > uppernode.Index && uppernode.Index > AnchorNode.Index) 
                        && !MLastNode.Equals(AnchorNode))
                        MColl.Remove(MLastNode);

                    // case 1 : begin and end nodes are parent
                    bool bParent = isParent(MLastNode, e.Node); // is AnchorNode parent (direct or not) of e.Node
                    if (!bParent)
                    {
                        bParent = isParent(bottomnode, uppernode);
                        if (bParent) // swap nodes
                        {
                            TreeNode t = uppernode;
                            uppernode = bottomnode;
                            bottomnode = t;
                        }
                    }
                    if (bParent)
                    {
                        TreeNode n = bottomnode;
                        while (n != uppernode.Parent)
                        {
                            if (!n.Equals(MLastNode))
                            {
                                if (!MColl.Contains(n)) MColl.Add(n);
                                else if (!n.Equals(AnchorNode)) MColl.Remove(n);
                            }
                            n = n.Parent;
                        }
                    }
                    // case 2 : nor the begin nor the end node are descendant one another
                    else
                    {
                        if ((uppernode.Parent == null && bottomnode.Parent == null) || (uppernode.Parent != null && uppernode.Parent.Nodes.Contains(bottomnode))) // are they siblings ?
                        {
                            int nIndexUpper = uppernode.Index;
                            int nIndexBottom = bottomnode.Index;
                            if (nIndexBottom < nIndexUpper) // reversed?
                            {
                                TreeNode t = uppernode;
                                uppernode = bottomnode;
                                bottomnode = t;
                                nIndexUpper = uppernode.Index;
                                nIndexBottom = bottomnode.Index;
                            }

                            TreeNode n = uppernode;
                            while (nIndexUpper <= nIndexBottom)
                            {
                                if (!n.Equals(MLastNode))
                                {
                                    if (!MColl.Contains(n) && !n.Equals(MLastNode)) MColl.Add(n);
                                    else if (!n.Equals(AnchorNode)) MColl.Remove(n);
                                }
                                n = n.NextNode;

                                nIndexUpper++;
                            } // end while

                        }
                        else
                        {
                            if (!uppernode.Equals(MLastNode))
                            {
                                if (!MColl.Contains(uppernode)) MColl.Add(uppernode);
                                else if (!uppernode.Equals(AnchorNode)) MColl.Remove(uppernode);
                            }
                            if (!bottomnode.Equals(MLastNode))
                            {
                                if (!MColl.Contains(bottomnode)) MColl.Add(bottomnode);
                                else if (!bottomnode.Equals(AnchorNode)) MColl.Remove(bottomnode);
                            }
                        }
                      }
                    Invalidate();
                    
                    MLastNode = e.Node; // let us chain several SHIFTs if we like it
                } // end if m_bShift
                else
                {
                    // in the case of a simple click, just add this item
                    if (MColl != null && MColl.Count > 0)
                    {
                        if (MColl.Count > 1)
                            Invalidate();
                        MColl.Clear();
                    }
                    if (MColl != null) MColl.Add(e.Node);
                }
            }
        }

        protected bool isParent(TreeNode parentNode, TreeNode childNode)
        {
            if (parentNode == childNode)
                return true;

            TreeNode n = childNode;
            bool bFound = false;
            while (!bFound && n != null)
            {
                n = n.Parent;
                bFound = (n == parentNode);
            }
            return bFound;
        }
    }
}
