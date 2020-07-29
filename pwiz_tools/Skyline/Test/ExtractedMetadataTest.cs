using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.MetadataExtraction;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ExtractedMetadataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestRegex()
        {
            string pattern = @"\b(\w+?)\s\1\b";
            string input = "This this is a nice day. What about this? This tastes good. I saw a a dog.";
            foreach (Match match in Regex.Matches(input, pattern, RegexOptions.IgnoreCase))
                Console.WriteLine("{0} (duplicates '{1}') at position {2}",
                    match.Value, match.Groups[1].Value, match.Index);
        }

        [TestMethod]
        public void TestReplace()
        {
            var input = "Now is the time for all good men to come to the aid of their country";

            var replace1 = Regex.Replace(input, @"([a-z])\1", @"_$1 $1 $1_");
            Console.WriteLine(replace1);
            var replace2 = Regex.Replace(input, @"([a-z])\1", @"_\1 \1 \1_");
            Console.WriteLine(replace2);
        }

        [TestMethod]
        public void TestMetadataRuleSerialization()
        {
            var ruleSet = new MetadataRuleSet("test", new []
            {
                new MetadataRule().ChangeSource(PropertyPath.Parse("foo"))
                    .ChangePattern("[a-z]")
                    .ChangeReplacement("$0")
                    .ChangeTarget(PropertyPath.Parse("bar"))
                
            });
            Deserializable(ruleSet);
        }

        public static void Deserializable<T>(T obj) where T: class
        {
            string expected = null;
            AssertEx.RoundTrip<T>(obj, ref expected);
            var xmlElementHelper = new XmlElementHelper<T>();
            using (var xmlReader = XmlReader.Create(new StringReader(expected)))
            {
                while (!xmlReader.IsStartElement())
                {
                    xmlReader.Read();
                }
                var obj2 = xmlElementHelper.Deserialize(xmlReader);
                Assert.AreEqual(obj, obj2);
            }
        }
    }
}
