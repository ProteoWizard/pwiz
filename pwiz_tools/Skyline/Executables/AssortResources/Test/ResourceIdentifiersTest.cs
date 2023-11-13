using System.Diagnostics;
using AssortResources;

namespace Test
{
    public class ResourceIdentifiersTest
    {
        private string testString = TestResources.ResourceIdentifiersTest_testString_This_is_a_test_string;
        [Fact]
        public void TestResourceIdentifiers()
        {
            var sourceFilePath = GetSourceFilePath();
            Assert.NotNull(sourceFilePath);
            Assert.True(File.Exists(sourceFilePath));
            var resourceFilePath = Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "TestResources.resx");
            Assert.True(File.Exists(resourceFilePath));
            var resourceIdentifiers = ResourceIdentifiers.FromPath(resourceFilePath);
            var references = resourceIdentifiers.GetReferencedNames(File.ReadAllText(sourceFilePath)).ToList();
            Assert.Equal(1, references.Count);
        }

        private string? GetSourceFilePath()
        {
            foreach (var frame in new StackTrace(true).GetFrames())
            {
                var filename = frame.GetFileName();
                if (true == frame.GetFileName()?.EndsWith("ResourceIdentifiersTest.cs"))
                {
                    return filename;
                }
            }

            return null;
        }
    }
}