/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for TricAlignersTest
    /// </summary>
    [TestClass]
    public class TricAlignersTest : AbstractUnitTest
    {
        const string TEST_ZIP_FILE = @"TestA\TricAlignerTest.zip";

        [TestMethod]
        public void TestKDE()
        {
            var tfd = new TestFilesDir(TestContext, TEST_ZIP_FILE);
            var csvPath = tfd.GetTestPath("kdeTutorialFit.csv"); 
            var xList = new List<double>();
            var yList = new List<double>();
            using (var reader = new StreamReader(csvPath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var split = line.Split(',');
                    var xval = double.Parse(split[0], CultureInfo.InvariantCulture);
                    var yval = double.Parse(split[1], CultureInfo.InvariantCulture);
                    xList.Add(xval);
                    yList.Add(yval);
                }    
            }

            var kde = new KdeAligner(0,1);
            kde.Train(xList.ToArray(), yList.ToArray());
            var meanSqError = 0d;
            var n = xList.Count;

            for(int i = 0; i < xList.Count; i ++)
            {
                var x = xList[i];
                var origY = yList[i];
                var fitY = kde.GetValue(x);
                Assert.IsTrue(Math.Abs(x - kde.GetValueReversed(fitY)) < 0.067);    // TODO(Max): This errors
                meanSqError += (fitY - origY)*(fitY - origY)/n;
            }

            Assert.IsTrue(Math.Abs(meanSqError- 0.738) < 0.001);
        }
    }
}
