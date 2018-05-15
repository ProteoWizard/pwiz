using System;
using System.Linq;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiUrl : RemoteUrl
    {
        public static readonly UnifiUrl EMPTY = new UnifiUrl(UrlPrefix);
        public static string UrlPrefix { get { return RemoteAccountType.UNIFI.Name + ":"; } }

        public UnifiUrl(string unifiUrl)
        {
            // ReSharper disable VirtualMemberCallInConstructor
            Init(ParseNameValueParameters(unifiUrl));
            // ReSharper restore VirtualMemberCallInConstructor
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

        public override RemoteAccountType AccountType
        {
            get { return RemoteAccountType.UNIFI; }
        }

        protected override NameValueParameters GetParameters()
        {
            var result = base.GetParameters();
            result.SetValue("id", Id);
            return result;
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra)
        {
            var account = Settings.Default.RemoteAccountList.FirstOrDefault(acct => acct.CanHandleUrl(this)) as UnifiAccount;
            if (account == null)
            {
                throw new RemoteServerException(string.Format("Cannot find account for username {0} and server {1}.", 
                    Username, ServerUrl));
            }
            string serverUrl = ServerUrl.Replace("://", "://" + account.Username + ":" + account.Password + "@");
            serverUrl += "/unifi/v1/sampleresults(" + Id + ")?";
            serverUrl += "identity=" + Uri.EscapeDataString(account.IdentityServer) + "&scope=" +
                         Uri.EscapeDataString(account.ClientScope) + "&secret=" +
                         Uri.EscapeDataString(account.ClientSecret);
            return new MsDataFileImpl(serverUrl, 0, LockMassParameters, simAsSpectra,
                requireVendorCentroidedMS1: CentroidMs1, requireVendorCentroidedMS2: CentroidMs2,
                ignoreZeroIntensityPoints: true);
        }
    }
}
