using ResourcesOrganizer.DataModel;

namespace Test
{
    [TestClass]
    public class SessionFactoryFactoryTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestCreateSessionFactory()
        {
            Assert.IsNotNull(TestContext.TestRunResultsDirectory);
            Assert.IsTrue(Directory.Exists(TestContext.TestRunResultsDirectory));
            var filePath = Path.Combine(TestContext.TestRunResultsDirectory, "test.db");

            using var sessionFactory = SessionFactoryFactory.CreateSessionFactory(filePath, true);
            Assert.IsTrue(File.Exists(filePath));
        }
    }
}