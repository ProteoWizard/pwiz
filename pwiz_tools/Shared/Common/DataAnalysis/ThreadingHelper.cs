using System;
using System.Threading;

namespace pwiz.Common.DataAnalysis
{
    public class CustomCancellationToken
    {
        public static readonly CustomCancellationToken NONE = new CustomCancellationToken(CancellationToken.None);

        public CustomCancellationToken(CancellationToken token, Func<bool> isCancelled = null)
        {
            Token = token;
            IsCancelled = isCancelled;
        }

        public bool IsCancellationRequested
        {
            get { return IsCancelled?.Invoke() ?? Token.IsCancellationRequested; }
        }

        public static implicit operator CustomCancellationToken(CancellationToken token)
        {
            return new CustomCancellationToken(token);
        }

        public CancellationToken Token { get; private set; }
        public Func<bool> IsCancelled { get; private set; }
    }

    public class ThreadingHelper
    {
        public static void CheckCanceled(CustomCancellationToken token)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException();
        }
    }
}
