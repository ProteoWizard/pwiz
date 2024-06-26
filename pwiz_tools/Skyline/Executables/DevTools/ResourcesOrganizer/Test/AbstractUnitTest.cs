using ResourcesOrganizer.ResourcesModel;

namespace Test
{
    public abstract class AbstractUnitTest
    {
        public TestContext TestContext { get; set; }

        protected void SaveManifestResources(Type type, string destination)
        {
            var assembly = type.Assembly;
            const string suffix = ".testfile";
            string prefix = type.FullName + ".";
            foreach (var manifestResourceName in assembly.GetManifestResourceNames())
            {
                if (manifestResourceName.StartsWith(prefix) && manifestResourceName.EndsWith(suffix))
                {
                    var target = manifestResourceName.Substring(prefix.Length,
                        manifestResourceName.Length - prefix.Length - suffix.Length);
                    using var stream = assembly.GetManifestResourceStream(manifestResourceName);
                    using var dest = File.OpenWrite(Path.Combine(destination, target));
                    stream!.CopyTo(dest);
                }
            }
        }

        protected void VerifyRoundTrip(ResourcesDatabase database, string path)
        {
            database.Save(path);
            var roundTrip = ResourcesDatabase.ReadDatabase(path);
            CollectionAssert.AreEquivalent(database.ResourcesFiles.Keys.ToList(),
                roundTrip.ResourcesFiles.Keys.ToList());
            foreach (var resourcesFileEntry in database.ResourcesFiles)
            {
                var resourcesFile = resourcesFileEntry.Value;
                Assert.IsTrue(roundTrip.ResourcesFiles.TryGetValue(resourcesFileEntry.Key, out var roundTripFile));
                CollectionAssert.AreEqual(resourcesFile.Entries.ToList(), roundTripFile.Entries.ToList());
            }
        }
    }
}
