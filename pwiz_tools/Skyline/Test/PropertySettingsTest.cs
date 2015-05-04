/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{        
    /// <summary>
    /// This is a test class for serialized propery settings.
    /// </summary>
    [TestClass]
    public class PropertySettingsTest : AbstractUnitTest
    {

        /// <summary>
        /// Test error handling in XML deserialization of <see cref="Server"/>.
        /// </summary>
        [TestMethod]
        public void SerializeServerTest()
        {
            const string validPanoramaServer = "http://128.208.10.133:8070/";
            // Valid first
            AssertEx.DeserializeNoError<Server>("<server uri=\"" + validPanoramaServer + "\" />", checkAgainstSkylineSchema:false);
            AssertEx.DeserializeNoError<Server>("<server uri=\"" + validPanoramaServer + "\" " +
                                                "username=\"\" password=\"\" />", checkAgainstSkylineSchema: false);
            AssertEx.DeserializeNoError<Server>("<server uri=\"" + validPanoramaServer + "\" " +
                                                "username=\"testuser3@panorama.org\" " +
                                                "password=\"testuser3\" />", checkAgainstSkylineSchema: false);

            // Failures
            AssertEx.DeserializeError<Server>("<server />");
            // A server url should always be provided.
            AssertEx.DeserializeError<Server>("<server " +
                                                "username=\"testuser3@panorama.org\" " +
                                                "password=\"testuser3\" />");
            // Bad URL
            AssertEx.DeserializeError<Server>("<server uri=\"w ww.google.com\" />");
            AssertEx.DeserializeError<Server>("<server uri=\"http://\" />");
        }


    }
}
