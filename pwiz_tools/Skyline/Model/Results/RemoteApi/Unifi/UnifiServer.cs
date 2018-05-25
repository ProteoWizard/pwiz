using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.RemoteApi.Unifi
{
    public class UnifiServer : Immutable
    {
        public static readonly UnifiServer DEFAULT = new UnifiServer("https://unifiapi.waters.com")
            .ChangeServerPort(50034).ChangeIdentityPort(50333);

        public UnifiServer(string serverUrl)
        {
            ServerUrl = serverUrl;
            ServerPort = 50034;
            IdentityPort = 50333;
        }
        public string ServerUrl { get; private set; }
        public int ServerPort { get; private set; }

        public UnifiServer ChangeServerPort(int port)
        {
            return ChangeProp(ImClone(this), im => im.ServerPort = port);
        }
        public int IdentityPort { get; private set; }

        public UnifiServer ChangeIdentityPort(int port)
        {
            return ChangeProp(ImClone(this), im => im.IdentityPort = port);
        }

        public string GetServerUrl()
        {
            return ServerUrl + ":" + ServerPort;
        }

        public string GetIdentityServerUrl()
        {
            return ServerUrl + ":" + IdentityPort;
        }
    }
}
