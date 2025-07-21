using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class EncryptedToken
    {
        public string Encrypted { get; private set; }
        public string Decrypted => !IsNullOrEmpty(this) ? CommonTextUtil.DecryptString(Encrypted) : "";
        public static bool IsNullOrEmpty(EncryptedToken token) => token == null || token.Encrypted.IsNullOrEmpty();

        private EncryptedToken(string token)
        {
            Encrypted = CommonTextUtil.EncryptString(token);
        }

        public static EncryptedToken FromString(string token) => new EncryptedToken(token);

        public static EncryptedToken FromEncryptedString(string encryptedToken)
        {
            var token = new EncryptedToken("");
            {
                token.Encrypted = encryptedToken;
            };
            return token;
        }

        public static EncryptedToken Empty => new EncryptedToken("");
    }
}
