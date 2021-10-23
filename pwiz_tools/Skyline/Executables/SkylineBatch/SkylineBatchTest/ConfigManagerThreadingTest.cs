using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SkylineBatchTest
{


    [TestClass]
    public class ConfigManagerThreadingTest
    {
        
        [TestMethod]
        public void TestThreadingAdd()
        {
            for (int i = 0; i < 100; i++)
                ThreadingAdd();
        }
        
        public void ThreadingAdd()
        {
            var configManager = TestUtils.GetTestConfigManager();
            var addingConfig = TestUtils.GetTestConfig("new");
            Exception firstThreadException = null;
            var threadStart = new ThreadStart(() =>
            {
                try
                {
                    configManager.UserAddConfig(addingConfig);
                }
                catch (Exception e)
                {
                    firstThreadException = firstThreadException ?? e;
                }
            });
            
            StartTestingThreads(threadStart, 15);
            configManager.GetSelectedLogger().Delete();
            Assert.AreEqual("one  two  three  new  ", configManager.ListConfigNames());
            Assert.IsTrue(firstThreadException != null, "Should have failed to add the second time, since no index selected");
            Assert.AreEqual("Configuration \"new\" already exists.\r\nPlease enter a unique name for the configuration.", firstThreadException.Message);
        }

        [TestMethod]
        public void TestThreadingMove()
        {
            for (int i = 0; i < 100; i++)
                ThreadingMove();
        }

        
        public void ThreadingMove()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(2);
            Exception firstThreadException = null;
            bool movedBoth = true;
            var threadStart = new ThreadStart(() =>
            {
                try
                {
                    if (!configManager.MoveSelectedConfig(true))
                        movedBoth = false;
                }
                catch (Exception e)
                {
                    firstThreadException = firstThreadException ?? e;
                }
            });
            
            StartTestingThreads(threadStart, 2);
            configManager.GetSelectedLogger().Delete();
            if (movedBoth)
            {
                Assert.AreEqual("three  one  two  ", configManager.ListConfigNames());
                Assert.AreEqual(0, configManager.State.BaseState.Selected);
            }
            else
            {
                Assert.AreEqual("one  three  two  ", configManager.ListConfigNames());
                Assert.AreEqual(1, configManager.State.BaseState.Selected);
            }
            var exceptionMessage = firstThreadException == null ? string.Empty : firstThreadException.Message;
            Assert.IsTrue(firstThreadException == null, "Unexpected Exception: " + exceptionMessage);
        }

        [TestMethod]
        public void TestThreadingRemove()
        {
            for (int i = 0; i < 100; i++)
            {
                ThreadingRemove();
            }
        }
        
        public void ThreadingRemove()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(2);
            Exception firstThreadException = null;
            bool deletedBoth = true;
            var threadStart = new ThreadStart(() =>
            {
                try
                {
                    if (!configManager.UserRemoveSelected())
                        deletedBoth = false;
                }
                catch (Exception e)
                {
                    firstThreadException = firstThreadException ?? e;
                }
            });
            StartTestingThreads(threadStart, 2);
            configManager.GetSelectedLogger().Delete();

            if (deletedBoth)
            {
                Assert.AreEqual("one  ", configManager.ListConfigNames());
                Assert.AreEqual(0, configManager.State.BaseState.Selected);
            }
            else
            {
                Assert.AreEqual("one  two  ", configManager.ListConfigNames());
                Assert.AreEqual(1, configManager.State.BaseState.Selected);
            }
            Assert.IsTrue(firstThreadException == null, "Should have removed configuration at 0 index twice with no exceptions.");
        }


        [TestMethod]
        public void TestThreadingReplace()
        {
            for (int i = 0; i < 10; i++)
            {
                ThreadingReplace();
            }
        }
        
        public void ThreadingReplace()
        {
            var configManager = TestUtils.GetTestConfigManager();
            configManager.SelectConfig(0);
            var random = new Random();
            var newConfig = TestUtils.GetTestConfig("new");
            string unexpectedExceptionMessage = null;

            var threadStart = new ThreadStart(() =>
            {
                Exception threadException = null;
                try
                {
                    configManager.SelectConfig(random.Next(0,2));
                    configManager.UserReplaceSelected(newConfig);
                }
                catch (Exception e)
                {
                    threadException = e;
                }

                var expectedErrorMessage =
                    "Configuration \"new\" already exists.\r\nPlease enter a unique name for the configuration.";
                if (threadException != null && !Equals(expectedErrorMessage, threadException.Message))
                    unexpectedExceptionMessage = string.Format("Expected exception: {0}\r\nActual: {1}", expectedErrorMessage, threadException.Message);
            });

            StartTestingThreads(threadStart, 200);
            configManager.GetSelectedLogger().Delete();
            if (unexpectedExceptionMessage != null)
                Assert.Fail(unexpectedExceptionMessage);
            var expectedConfigLists = new List<string> {"new  two  three  ", "one  new  three  ", "one  two  new  "};
            Assert.IsTrue(expectedConfigLists.Contains(configManager.ListConfigNames()), "Unexpected config list: " + configManager.ListConfigNames());
        }

        public void StartTestingThreads(ThreadStart operation, int numThreads)
        {
            var testingThreads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                testingThreads.Add(new Thread(operation));
            }

            foreach (var thread in testingThreads)
            {
                thread.Start();
            }

            var stillRunning = true;
            while (stillRunning)
            {
                stillRunning = false;
                foreach (var thread in testingThreads)
                {
                    if (thread.IsAlive)
                    {
                        stillRunning = true;
                        break;
                    }
                }
            }
        }
    }
}
