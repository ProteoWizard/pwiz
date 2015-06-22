using System;
using System.Collections.Generic;
using AutoQC;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AutoQCTest
{
    [TestClass]
    public class MainSettingsTest
    {
        [TestMethod]
        public void TestAddArchiveArgs()
        {
            MainSettings mainSettings = new MainSettings();
            mainSettings.SkylineFilePath = @"C:\Users\vsharma\Test_file.sky";
            var date = new DateTime(2015, 6, 17);
            mainSettings.LastArchivalDate = date;
            
            var args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.IsNull(args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);

            date = date.AddMonths(1); // 07/17/2015
            var archiveArg = string.Format("--share-zip={0}", "Test_file_2015_06.sky.zip");
            args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.AreEqual(archiveArg, args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);

            date = date.AddYears(1); // 06/17/2016
            archiveArg = string.Format("--share-zip={0}", "Test_file_2015_07.sky.zip");
            args = mainSettings.GetArchiveArgs(mainSettings.LastArchivalDate, date);
            Assert.AreEqual(archiveArg, args);
            Assert.AreEqual(date, mainSettings.LastArchivalDate);
        }
    }
}
