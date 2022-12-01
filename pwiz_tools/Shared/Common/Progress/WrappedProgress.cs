using System.Threading;

namespace pwiz.Common.Progress
{
    public class WrappedProgress : AbstractProgress
    {
        public WrappedProgress(IProgress parent) : this(parent, parent.CancellationToken)
        {
}

        public WrappedProgress(IProgress parent, CancellationToken newCancellationToken) : base(newCancellationToken)
        {
            Parent = parent;
        }

        protected IProgress Parent { get; }

        public override string Message
        {
            set
            {
                Parent.Message = value;
            }
        }

        public override double Value
        {
            set
            {
                Parent.Value = value;
            }
        }
    }
}
