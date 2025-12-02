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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData.RemoteApi.Ardia;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class EncryptedTokenTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestEncryptedToken()
        {
            const string tokenString = "token value";
            var tokenFromDecrypted = EncryptedToken.FromString(tokenString);
            Assert.AreEqual(tokenString, tokenFromDecrypted.Decrypted);
            Assert.IsFalse(tokenFromDecrypted.IsNullOrEmpty());

            var tokenFromEncrypted = EncryptedToken.FromEncryptedString(tokenFromDecrypted.Encrypted);
            Assert.AreEqual(tokenString, tokenFromEncrypted.Decrypted);
            Assert.IsFalse(tokenFromEncrypted.IsNullOrEmpty());
            
            Assert.IsTrue(((EncryptedToken)null).IsNullOrEmpty());
            Assert.IsTrue(EncryptedToken.Empty.IsNullOrEmpty());
        }
    }
}
