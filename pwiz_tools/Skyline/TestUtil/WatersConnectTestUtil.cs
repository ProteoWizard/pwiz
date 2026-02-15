/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using pwiz.CommonMsData.RemoteApi.WatersConnect;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Helper methods for testing the waters_connect server.
    /// In order for waters_connect tests to be enabled, you must have an environment variable "WC_PASSWORD".
    /// </summary>
    public static class WatersConnectTestUtil
    {
        public static WatersConnectAccount GetTestAccount()
        {
            var password = Environment.GetEnvironmentVariable("WC_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                return null;
            }
            return (WatersConnectAccount)WatersConnectAccount.DEV_DEFAULT.ChangeUsername("skyline")
                .ChangePassword(password);

        }

        public static bool EnableWatersConnectTests
        {
            get
            {
                return GetTestAccount() != null;
            }
        }
    }
}
