/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Immutable;
using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    [TestClass]
    public class ResourcesFileTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestResourcesFile()
        {
            string runDirectory = SaveManifestResources(typeof(ResourcesFileTest));
            var file = ResourcesFile.Read(Path.Combine(runDirectory, "Resources.resx"), "Resources.resx");
            Assert.AreNotEqual(0, file.Entries.Count);
        }

        [TestMethod]
        public void TestResourcesDatabase()
        {
            string runDirectory = SaveManifestResources(typeof(ResourcesFileTest));
            var file = ResourcesFile.Read(Path.Combine(runDirectory, "Resources.resx"), "Resources.resx");
            var resourcesDatabase = new ResourcesDatabase
            {
                ResourcesFiles = ImmutableDictionary<string, ResourcesFile>.Empty.Add("test", file)
            };
            Assert.AreNotEqual(0, resourcesDatabase.GetInvariantResources().Count());
            var dbPath = Path.Combine(runDirectory, "resources.db");
            resourcesDatabase.Save(dbPath);
            var compare = ResourcesDatabase.ReadDatabase(dbPath);
            var expectedInvariantResources =
                resourcesDatabase.GetInvariantResources().Select(grouping => grouping.Key).ToList();
            var roundTripInvariantResources = compare.GetInvariantResources().Select(grouping => grouping.Key).ToList();
            CollectionAssert.AreEqual(expectedInvariantResources, roundTripInvariantResources);
        }

        [TestMethod]
        public void TestInvariantKeyFile()
        {
            string runDirectory = SaveManifestResources(typeof(ResourcesFileTest));
            var file = ResourcesFile.Read(Path.Combine(runDirectory, "Resources.resx"), "Resources.resx");
            var resourcesDatabase = ResourcesDatabase.Empty with
            {
                ResourcesFiles = ImmutableDictionary<string, ResourcesFile>.Empty.Add("test", file)
            };
            var dbPath = Path.Combine(runDirectory, "resources.db");
            resourcesDatabase.Save(dbPath);
            var compare = ResourcesDatabase.ReadDatabase(dbPath);
            var expectedInvariantResources =
                resourcesDatabase.GetInvariantResources().Select(grouping => grouping.Key).ToList();
            var roundTripInvariantResources = compare.GetInvariantResources().Select(grouping => grouping.Key).ToList();
            CollectionAssert.AreEqual(expectedInvariantResources, roundTripInvariantResources);
        }
    }
}
