using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Persister.Entity;
using pwiz.Skyline.Controls;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LongWaitDlgResizeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLongWaitDlgResize()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            int[] step = new int[1];
            ShowDialog<LongWaitDlg>(() =>
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.PerformWork(SkylineWindow, 1, longWaitBroker =>
                    {
                        longWaitBroker.Message =
                            "TheQuickBrownFoxJumpsOverTheFirstLazyDogThenTheQuickBrownFoxJumpsOverTheSecondLazyDog";
                        lock (step)
                        {
                            while (step[0] < 1)
                            {
                                Monitor.Wait(step);
                            }
                        }
                    });
                }
            });
            PauseTest();
            lock (step)
            {
                step[0] = 1;
                Monitor.Pulse(step);
            }
        }
    }
}
