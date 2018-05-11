using System;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiUrl : RemoteUrl
    {
        public static readonly UnifiUrl EMPTY = new UnifiUrl(UrlPrefix);
        public static string UrlPrefix { get { return RemoteAccountType.UNIFI.Name + ":"; } }

        public UnifiUrl(string unifiUrl)
        {
            Init(ParseNameValueParameters(unifiUrl));
        }

        protected override void Init(NameValueParameters nameValueParameters)
        {
            base.Init(nameValueParameters);
            Id = nameValueParameters.GetValue("id");
        }

        public string Id { get; private set; }

        public UnifiUrl ChangeId(string id)
        {
            return ChangeProp(ImClone(this), im => im.Id = id);
        }
        

        
        public override bool IsWatersLockmassCorrectionCandidate()
        {
            return true;
        }

        public override DateTime GetFileLastWriteTime()
        {
            return default(DateTime);
        }

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.UNIFI; }
        }
    }
}
