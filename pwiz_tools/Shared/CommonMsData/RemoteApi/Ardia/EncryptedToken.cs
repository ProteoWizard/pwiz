/*
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class EncryptedToken
    {
        public string Encrypted { get; }
        public string Decrypted => !this.IsNullOrEmpty() ? CommonTextUtil.DecryptString(Encrypted) : string.Empty;

        private EncryptedToken(string encryptedToken)
        {
            Encrypted = encryptedToken;
        }

        public static EncryptedToken FromString(string token) => new EncryptedToken(CommonTextUtil.EncryptString(token));

        public static EncryptedToken FromEncryptedString(string encryptedToken) => new EncryptedToken(encryptedToken);

        public static EncryptedToken Empty => new EncryptedToken(string.Empty);
    }

    public static class Extensions
    {
        public static bool IsNullOrEmpty(this EncryptedToken token)
        {
            return token == null || token.Encrypted.IsNullOrEmpty();
        }
    }
}
