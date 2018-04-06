//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//

using System.IO;
using BumberDash.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BumberDash.Model;
using System.Windows.Forms;

namespace Tests
{
    
    
    /// <summary>
    ///This is a test class for QueueFormTest and is intended
    ///to contain all QueueFormTest Unit Tests
    ///</summary>
    [TestClass()]
    public class QueueFormTest
    {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bumberdash");
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            var dataFile = Path.Combine(root, "Bumbershoot.db");

            if (File.Exists(dataFile))
            {
                File.Copy(dataFile, Path.Combine(root, "Bumbershoot-backup.db"));
                File.Delete(dataFile);
            }
        }

        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bumberdash");
            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);
            var dataFile = Path.Combine(root, "Bumbershoot.db");
            var backupData = Path.Combine(root, "Bumbershoot-backup.db");

            if (File.Exists(backupData))
            {
                File.Delete(dataFile);
                File.Copy(backupData, dataFile);
            }
        }

        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion


        ///// <summary>
        /////A test for QueueForm Constructor
        /////</summary>
        //[TestMethod()]
        //public void QueueFormConstructorTest()
        //{
        //    QueueForm target = new QueueForm();
        //}

        ///// <summary>
        /////A test for aboutToolStripMenuItem_Click
        /////</summary>
        //[TestMethod()]
        //[DeploymentItem("BumberDash.exe")]
        //public void aboutToolStripMenuItem_ClickTest()
        //{
        //    QueueForm_Accessor target = new QueueForm_Accessor(); //Initialize to an appropriate value
        //    object sender = null; //Initialize to an appropriate value
        //    EventArgs e = null; //Initialize to an appropriate value
        //    target.aboutToolStripMenuItem_Click(sender, e);
        //    Assert.Inconclusive("A method that does not return a value cannot be verified.");
        //}

        

    }
}
