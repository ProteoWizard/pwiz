/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.CommonMsData.RemoteApi.Ardia;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Helper methods for testing the Ardia server.
    /// In order for Ardia tests to be enabled, you must have environment variables "ARDIA_PASSWORD" and "ARDIA_PASSWORD_1ROLE";
    /// </summary>
    public static class ArdiaTestUtil
    {
        private const string BASE_URL = "https://ardia-core-int.cmdtest.thermofisher.com/";

        public enum AccountType
        {
            MultiRole,
            SingleRole
        }

        public static ArdiaAccount GetTestAccount(AccountType type = AccountType.MultiRole)
        {
            string envVarName = type == AccountType.MultiRole ? "ARDIA_PASSWORD" : "ARDIA_PASSWORD_1ROLE";

            var password = Environment.GetEnvironmentVariable(envVarName);
            if (string.IsNullOrWhiteSpace(password))
                return null;

            switch (type)
            {
                case AccountType.MultiRole:
                    return (ArdiaAccount)ArdiaAccount.DEFAULT.ChangeTestingOnly_NotSerialized_Role("SkylineTester")
                        .ChangeTestingOnly_NotSerialized_Username("matt.chambers42@gmail.com")
                        .ChangeTestingOnly_NotSerialized_Password(password)
                        .ChangeUsername("Testing_FAKE_ArdiaUser_MultiRole")
                        .ChangeServerUrl(BASE_URL);

                case AccountType.SingleRole:
                    return (ArdiaAccount)ArdiaAccount.DEFAULT

                        .ChangeTestingOnly_NotSerialized_Username("chambem2@uw.edu")

                        //  The Client Registration will fail with HTTP Status 403 due to TestingOnly_NotSerialized_Role configuration where NOT Enabled: "Generate an activation code and/or directly register client"
                        //  TestingOnly_NotSerialized_Role SkylineTeser_NoClientRegist - No Client Registration
                        // .ChangeTestingOnly_NotSerialized_Username("djaschob@u.washington.edu")

                        .ChangeTestingOnly_NotSerialized_Password(password)

                        .ChangeUsername("Testing_FAKE_ArdiaUser_SingleRole")

                        .ChangeServerUrl(BASE_URL);

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static bool EnableArdiaTests
        {
            get
            {
                return GetTestAccount() != null;
            }
        }
    }
}
