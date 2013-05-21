using System.Linq;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds <see cref="DocNode"/> elements with a certain quality, excluding
    /// child nodes with the same quality.
    /// </summary>
    public abstract class AbstractDocNodeFinder : AbstractFinder
    {
        public override FindMatch Match(BookmarkEnumerator bookmarkEnumerator)
        {
            if (bookmarkEnumerator.CurrentChromInfo == null)
            {
                var nodePep = bookmarkEnumerator.CurrentDocNode as PeptideDocNode;
                if (nodePep != null)
                {
                    return IsMatch(nodePep) ? new FindMatch(DisplayName) : null;
                }
                var nodeGroup = bookmarkEnumerator.CurrentDocNode as TransitionGroupDocNode;
                if (nodeGroup != null)
                {
                    // Only report transitions when the precursor has lib info
                    var nodeParent = bookmarkEnumerator.Document.FindNode(bookmarkEnumerator.IdentityPath.Parent)
                                     as PeptideDocNode;
                    return !IsMatch(nodeParent) && IsMatch(nodeGroup) ? new FindMatch(DisplayName) : null;
                }
                var nodeTran = bookmarkEnumerator.CurrentDocNode as TransitionDocNode;
                if (nodeTran != null)
                {
                    // Only report transitions when the precursor has lib info
                    var nodeParent = bookmarkEnumerator.Document.FindNode(bookmarkEnumerator.IdentityPath.Parent)
                                     as TransitionGroupDocNode;
                    return !IsMatch(nodeParent) && IsMatch(nodeTran) ? new FindMatch(DisplayName) : null;
                }
            }
            return null;            
        }

        protected virtual bool IsMatch(PeptideGroupDocNode nodePepGroup)
        {
            return false;
        }

        protected virtual bool IsMatch(PeptideDocNode nodePep)
        {
            return false;
        }

        protected virtual bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return false;
        }

        protected virtual bool IsMatch(TransitionDocNode nodeTran)
        {
            return false;
        }
    }
}