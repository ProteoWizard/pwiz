using System;
using System.Collections.Generic;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiSession : RemoteSession
    {

        public UnifiSession(UnifiAccount account) : base(account)
        {
            
        }

        public UnifiAccount UnifiAccount { get { return (UnifiAccount) Account; } }

        public override bool AsyncFetchContents(RemoteUrl chorusUrl, out RemoteServerException remoteException)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<RemoteItem> ListContents(MsDataFileUri parentUrl)
        {
            throw new NotImplementedException();
        }

        public override void RetryFetchContents(RemoteUrl chorusUrl)
        {
            throw new NotImplementedException();
        }
    }
}
