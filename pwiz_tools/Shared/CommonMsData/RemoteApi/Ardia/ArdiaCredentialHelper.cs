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

using System;
using System.Collections.Generic;
using pwiz.Common.SystemUtil;

namespace pwiz.CommonMsData.RemoteApi.Ardia
{
    public class ArdiaCredentialHelper
    {
        private static readonly IDictionary<string, string> CACHE = new Dictionary<string, string>();

        public static EncryptedToken GetToken(ArdiaAccount account)
        {
            Assume.IsNotNull(account);

            return account.Token;
        }

        public static void SetToken(ArdiaAccount account, string token)
        {
            Assume.IsNotNull(account);

            account.Token = EncryptedToken.FromString(token);
        }

        public static string GetApplicationCode(ArdiaAccount account)
        {
            Assume.IsNotNull(account);

            var ardiaUrl = account.GetRootArdiaUrl();

            // NOTE: settings for the Ardia account are keyed using a hostname different from the
            //       URLs use to actually call the Ardia API. So, use this hostname to read settings
            //       but do not use to call the API.
            var key = new Uri(ardiaUrl.ServerUrl).Host;

            return CACHE.TryGetValue(key, out var applicationCode) ? applicationCode : null;
        }

        public static void SetApplicationCode(string hostUrl, string applicationCode)
        {
            CACHE[hostUrl] = applicationCode;
        }

        /// <summary>
        /// Reset the token for this account. This simple helper exists to avoid making the <see cref="ArdiaAccount.Token"/> setter public.
        /// </summary>
        public static void ClearToken(ArdiaAccount account)
        {
            account.Token = null;
        }

        private ArdiaCredentialHelper() {}
    }
}
