//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker.DataModel;
using NHibernate.Linq;

namespace Test
{
    /// <summary>
    /// Summary description for TestQueryReader
    /// </summary>
    [TestClass]
    public class TestQueryReader
    {
        // shared session between TestQueryReader methods
        public NHibernate.ISession session;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            TestModel.ClassInitialize(testContext);
            File.Copy("testModel.idpDB", "testQueryReader.idpDB");
        }

        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void ClassCleanup() { }

        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void TestInitialize ()
        {
            var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testQueryReader.idpDB", false, false);
            session = sessionFactory.OpenSession();
        }

        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void TestCleanup() { }

        [TestMethod]
        public void TestNhibernateHqlQuery ()
        {
            var query = session.CreateQuery("SELECT psm FROM PeptideSpectrumMatch psm ORDER BY psm.Spectrum, psm.Id");
            int rowCount = 90;

            // sanity check
            Assert.AreEqual(rowCount, session.Query<PeptideSpectrumMatch>().Count());

            NHibernateQueryReader queryReader;
            QueryPage queryPage;
            string rowArray;

            // test at default page size of 1000 (all 90 psms on one page)
            queryReader = new NHibernateQueryReader(query, rowCount);
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(1, queryReader.PageCount);
            Assert.AreEqual(1000, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(rowCount, queryPage.Rows.Count);
            Assert.IsInstanceOfType(queryPage.Rows[0], typeof(PeptideSpectrumMatch));

            // test at page size of 30 (3 pages)
            queryReader.PageSize = 30;
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(3, queryReader.PageCount);
            Assert.AreEqual(30, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("1 2 3 46 47 48 4 5 6 49 50 51"));
            queryPage = queryReader.GetPage(1);
            Assert.AreEqual(1, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("16 17 18 61 62 63 19 20 21 64 65 66"));
            queryPage = queryReader.GetPage(2);
            Assert.AreEqual(2, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("31 32 33 76 77 78 34 35 36 79 80 81"));

            // test at page size of 40 (3 pages)
            queryReader.PageSize = 40;
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(3, queryReader.PageCount);
            Assert.AreEqual(40, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(40, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("1 2 3 46 47 48 4 5 6 49 50 51"));
            queryPage = queryReader.GetPage(1);
            Assert.AreEqual(1, queryPage.PageIndex);
            Assert.AreEqual(40, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("65 66 22 23 24 67 68 69 25 26 27 70"));
            queryPage = queryReader.GetPage(2);
            Assert.AreEqual(2, queryPage.PageIndex);
            Assert.AreEqual(10, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<PeptideSpectrumMatch>().Select(o => o.Id.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("42 85 86 87 43 44 45 88 89 90"));

            // test getting page by row index
            queryPage = queryReader.GetPageForRow(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            queryPage = queryReader.GetPageForRow(39);
            Assert.AreEqual(0, queryPage.PageIndex);
            queryPage = queryReader.GetPageForRow(40);
            Assert.AreEqual(1, queryPage.PageIndex);
            queryPage = queryReader.GetPageForRow(79);
            Assert.AreEqual(1, queryPage.PageIndex);
            queryPage = queryReader.GetPageForRow(80);
            Assert.AreEqual(2, queryPage.PageIndex);
            queryPage = queryReader.GetPageForRow(89);
            Assert.AreEqual(2, queryPage.PageIndex);

            // test returning multiple objects
            query = session.CreateQuery("SELECT psm, psm.Spectrum FROM PeptideSpectrumMatch psm ORDER BY psm.Spectrum, psm.Id");
            queryReader = new NHibernateQueryReader(query, rowCount);
            queryPage = queryReader.GetPage(0);
            Assert.IsInstanceOfType(queryPage.Rows[0], typeof(object[]));
            Assert.IsInstanceOfType((queryPage.Rows[0] as object[])[0], typeof(PeptideSpectrumMatch));
            Assert.IsInstanceOfType((queryPage.Rows[0] as object[])[1], typeof(Spectrum));
        }

        [TestMethod]
        public void TestNhibernateSqlQuery ()
        {
            var query = session.CreateSQLQuery("SELECT Id FROM PeptideSpectrumMatch ORDER BY Spectrum, Id");
            int rowCount = 90;

            // sanity check
            Assert.AreEqual(rowCount, session.Query<PeptideSpectrumMatch>().Count());

            NHibernateQueryReader queryReader;
            QueryPage queryPage;
            string rowArray;

            // test at default page size of 1000 (all 90 psms on one page)
            queryReader = new NHibernateQueryReader(query, rowCount);
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(1, queryReader.PageCount);
            Assert.AreEqual(1000, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(rowCount, queryPage.Rows.Count);
            Assert.IsInstanceOfType(queryPage.Rows[0], typeof(Int64));

            // test at page size of 30 (3 pages)
            queryReader.PageSize = 30;
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(3, queryReader.PageCount);
            Assert.AreEqual(30, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("1 2 3 46 47 48 4 5 6 49 50 51"));
            queryPage = queryReader.GetPage(1);
            Assert.AreEqual(1, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("16 17 18 61 62 63 19 20 21 64 65 66"));
            queryPage = queryReader.GetPage(2);
            Assert.AreEqual(2, queryPage.PageIndex);
            Assert.AreEqual(30, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("31 32 33 76 77 78 34 35 36 79 80 81"));

            // test at page size of 40 (3 pages)
            queryReader.PageSize = 40;
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(3, queryReader.PageCount);
            Assert.AreEqual(40, queryReader.PageSize);
            queryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, queryPage.PageIndex);
            Assert.AreEqual(40, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("1 2 3 46 47 48 4 5 6 49 50 51"));
            queryPage = queryReader.GetPage(1);
            Assert.AreEqual(1, queryPage.PageIndex);
            Assert.AreEqual(40, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("65 66 22 23 24 67 68 69 25 26 27 70"));
            queryPage = queryReader.GetPage(2);
            Assert.AreEqual(2, queryPage.PageIndex);
            Assert.AreEqual(10, queryPage.Rows.Count);
            rowArray = String.Join(" ", queryPage.Rows.Cast<long>().Select(o => o.ToString()).ToArray());
            Assert.IsTrue(rowArray.StartsWith("42 85 86 87 43 44 45 88 89 90"));

            // test returning multiple objects
            query = session.CreateSQLQuery("SELECT Id, QValue FROM PeptideSpectrumMatch ORDER BY Spectrum, Id");
            queryReader = new NHibernateQueryReader(query, rowCount);
            queryPage = queryReader.GetPage(0);
            Assert.IsInstanceOfType(queryPage.Rows[0], typeof(object[]));
            Assert.IsInstanceOfType((queryPage.Rows[0] as object[])[0], typeof(Int64));
            Assert.IsInstanceOfType((queryPage.Rows[0] as object[])[1], typeof(Double));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestGetPageArgumentOutOfRangeUnderflow ()
        {
            var query = session.CreateSQLQuery("SELECT Id FROM PeptideSpectrumMatch");
            var queryReader = new NHibernateQueryReader(query, 90) { PageSize = 30 };
            queryReader.GetPage(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestGetPageArgumentOutOfRangeOverflow ()
        {
            var query = session.CreateSQLQuery("SELECT Id FROM PeptideSpectrumMatch");
            var queryReader = new NHibernateQueryReader(query, 90) { PageSize = 30 };
            queryReader.GetPage(4);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestGetPageForRowArgumentOutOfRangeUnderflow ()
        {
            var query = session.CreateQuery("SELECT psm FROM PeptideSpectrumMatch psm");
            var queryReader = new NHibernateQueryReader(query, 90) { PageSize = 30 };
            queryReader.GetPageForRow(-1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TestGetPageForRowArgumentOutOfRangeOverflow ()
        {
            var query = session.CreateSQLQuery("SELECT Id FROM PeptideSpectrumMatch");
            var queryReader = new NHibernateQueryReader(query, 90) { PageSize = 30 };
            queryReader.GetPageForRow(90);
        }

        [TestMethod]
        public void TestQueryPageCache ()
        {
            var query = session.CreateSQLQuery("SELECT Id FROM PeptideSpectrumMatch ORDER BY Spectrum, Id");
            int rowCount = 90;

            // sanity check
            Assert.AreEqual(rowCount, session.Query<PeptideSpectrumMatch>().Count());

            // test at page size of 20 (5 pages)
            var queryReader = new QueryPageCache(query, rowCount) {PageSize = 20, CacheSize = 2};
            Assert.AreEqual(rowCount, queryReader.RowCount);
            Assert.AreEqual(5, queryReader.PageCount);
            Assert.AreEqual(20, queryReader.PageSize);

            // populate the cache with 2 pages
            var firstQueryPage = queryReader.GetPage(0);
            Assert.AreEqual(0, firstQueryPage.PageIndex);
            Assert.AreEqual(20, firstQueryPage.Rows.Count);
            var lastQueryPage = queryReader.GetPage(4);
            Assert.AreEqual(4, lastQueryPage.PageIndex);
            Assert.AreEqual(10, lastQueryPage.Rows.Count);

            // test that these pages are cached
            Assert.AreSame(firstQueryPage, queryReader.GetPage(0));
            Assert.AreSame(lastQueryPage, queryReader.GetPage(4));

            // bump the LRU page (firstQueryPage) out of the cache
            var secondQueryPage = queryReader.GetPage(1);
            Assert.AreEqual(1, secondQueryPage.PageIndex);
            Assert.AreEqual(20, secondQueryPage.Rows.Count);

            Assert.AreSame(lastQueryPage, queryReader.GetPage(4));
            Assert.AreSame(secondQueryPage, queryReader.GetPage(1));

            // bump the LRU page (lastQueryPage) out of the cache
            Assert.AreNotSame(firstQueryPage, queryReader.GetPage(0));
            Assert.AreSame(secondQueryPage, queryReader.GetPage(1));
            Assert.AreNotSame(lastQueryPage, queryReader.GetPage(4));
        }
    }
}
