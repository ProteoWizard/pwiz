using System;
using System.Net.Http;
using IdentityModel.Client;
using pwiz.Skyline.Model.Results.RemoteApi;

namespace pwiz.SkylineTestUtil.TestDoubles
{
    public class RecordingHttpInterface : IRemoteHttpInterface
    {
        public RecordingHttpInterface(IRemoteHttpInterface innerInterface)
        {
            InnerInterface = innerInterface;
        }

        public IRemoteHttpInterface InnerInterface { get; private set; }

        public string Get(HttpClient httpClient, Uri requestUri)
        {
            return InnerInterface.Get(httpClient, requestUri);
        }

        public TokenResponse RequestResourceOwnerPassword(TokenClient tokenClient, string username, string password,
            string clientScope)
        {
            return InnerInterface.RequestResourceOwnerPassword(tokenClient, username, password, clientScope);
        }
    }
}
