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
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(ResourcesFileTest), runDirectory);
            var file = ResourcesFile.Read(Path.Combine(runDirectory, "Resources.resx"), "Resources.resx");
            Assert.AreNotEqual(0, file.Entries.Count);
        }

        [TestMethod]
        public void TestResourcesDatabase()
        {
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(ResourcesFileTest), runDirectory);
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
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(ResourcesFileTest), runDirectory);
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
