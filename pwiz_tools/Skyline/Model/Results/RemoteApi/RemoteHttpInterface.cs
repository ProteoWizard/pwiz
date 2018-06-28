using System;
using System.Net.Http;
using IdentityModel.Client;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class RemoteHttpInterface
    {
        public static IRemoteHttpInterface Instance { get; private set; }

        static RemoteHttpInterface()
        {
            Instance = new Impl();
        }

        public void ExecuteWithInterface(IRemoteHttpInterface remoteHttpInterface, Action action)
        {
            var oldInstance = Instance;
            try
            {
                action();
            }
            finally
            {
                Instance = oldInstance;
            }
        }
        
        private class Impl : IRemoteHttpInterface
        {
            public string Get(HttpClient httpClient, Uri requestUri)
            {
                return httpClient.GetAsync(requestUri).Result.Content.ReadAsStringAsync().Result;
            }

            public TokenResponse RequestResourceOwnerPassword(TokenClient tokenClient, string username, string password,
                string clientScope)
            {
                return tokenClient.RequestResourceOwnerPasswordAsync(username, password, clientScope).Result;
            }
        }
    }
}
