/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AnalyticsTest : AbstractUnitTest
    {
        [TestMethod]
        public void SendGa4AnalyticsHitTest()
        {
            int httpStatus = Program.SendGa4AnalyticsHit(out var responseStr, true);
            if (httpStatus == 200)
                Console.WriteLine(@"Unexpected response body from SendGa4AnalyticsHit: " + responseStr);
            CollectionAssert.Contains(new [] {200, 204}, httpStatus, "SendGa4AnalyticsHit expected 200 or 204, responded with {0}", httpStatus);
        }
    }
}