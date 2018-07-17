/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Database;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class SqliteOperationsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTableExists()
        {
            using (var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = ":memory:"
            }.ToString()))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE Table1(Id INTEGER)";
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE TABLE \"Table With Space\"(Id INTEGER)";
                    cmd.ExecuteNonQuery();
                }
                Assert.IsTrue(SqliteOperations.TableExists(connection, "Table1"));
                Assert.IsFalse(SqliteOperations.TableExists(connection, "Table2"));
                Assert.IsTrue(SqliteOperations.TableExists(connection, "Table With Space"));
                Assert.IsFalse(SqliteOperations.TableExists(connection, "Other Table With Space"));
            }
        }
    }
}
