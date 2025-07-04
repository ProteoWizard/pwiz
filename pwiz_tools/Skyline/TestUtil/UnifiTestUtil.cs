﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.CommonMsData.RemoteApi.Unifi;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Helper methods for testing the Unifi server.
    /// In order for Unifi tests to be enabled, you must have an environment variable "UNIFI_PASSWORD".
    /// </summary>
    public static class UnifiTestUtil
    {
        public static UnifiAccount GetTestAccount()
        {
            var password = Environment.GetEnvironmentVariable("UNIFI_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                return null;
            }
            return (UnifiAccount)UnifiAccount.DEFAULT.ChangeUsername("msconvert")
                .ChangePassword(password);

        }

        public static bool EnableUnifiTests
        {
            get
            {
                return GetTestAccount() != null;
            }
        }
    }
}
