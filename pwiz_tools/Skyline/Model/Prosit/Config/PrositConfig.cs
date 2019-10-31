using System.Xml.Serialization;
using Grpc.Core;

namespace pwiz.Skyline.Model.Prosit.Config
{
    public class PrositConfig
    {
        public string Server { get; set; }
        public string RootCertificate { get; set; }
        public string ClientCertificate { get; set; }
        public string ClientKey { get; set; }

        public Channel CreateChannel()
        {
            return new Channel(Server, GetChannelCredentials());
        }

        public ChannelCredentials GetChannelCredentials()
        {
            if (string.IsNullOrEmpty(RootCertificate))
            {
                return ChannelCredentials.Insecure;
            }

            if (string.IsNullOrEmpty(ClientCertificate))
            {
                return new SslCredentials(RootCertificate);
            }
            return new SslCredentials(RootCertificate, new KeyCertificatePair(ClientCertificate, ClientKey));
        }


        public static PrositConfig GetPrositConfig()
        {
            using (var stream =
                typeof(PrositConfig).Assembly.GetManifestResourceStream(typeof(PrositConfig), "PrositConfig.xml"))
            {
                var xmlSerializer = new XmlSerializer(typeof(PrositConfig));
                return (PrositConfig) xmlSerializer.Deserialize(stream);
            }
        }
    }
}
