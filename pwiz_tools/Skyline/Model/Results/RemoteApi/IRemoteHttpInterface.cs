using System;
using System.Net.Http;
using System.Web;
using IdentityModel.Client;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public interface IRemoteHttpInterface
    {
        string Get(HttpClient httpClient, Uri requestUri);
        TokenResponse RequestResourceOwnerPassword(TokenClient tokenClient, string username, string password, string clientScope);
    }
}
