/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Test
{
    public class BaseTest
    {
        private TestContext _testContextInstance;
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
// ReSharper disable ConvertToAutoProperty
        public TestContext TestContext
        {
            get
            {
                return _testContextInstance;
            }
            set
            {
                _testContextInstance = value;
            }
        }
        // ReSharper restore ConvertToAutoProperty

        public String FindDirectory(String name)
        {
            for (String directory = TestContext.TestDir; null != directory && directory.Length > 10; directory = Path.GetDirectoryName(directory))
            {
                String testDataDirectory = Path.Combine(directory, name);
                if (Directory.Exists(testDataDirectory))
                {
                    return testDataDirectory;
                }
            }
            return null;
        }

        public String GetDataDirectory()
        {
            return FindDirectory("TestData");
        }
        public void CopyDirectory(String sourceDirectory, String destPath)
        {
            // ReSharper disable AssignNullToNotNullAttribute
            String destDirectory = Path.Combine(destPath, Path.GetFileName(sourceDirectory));
            Directory.CreateDirectory(destDirectory);
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destDirectory, Path.GetFileName(file)));
            }
            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(directory, destDirectory);
            }
            // ReSharper restore AssignNullToNotNullAttribute
        }

        public void FreshenDirectory(String sourceDirectory, String destDirectory)
        {
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                String destFile = Path.Combine(destDirectory, Path.GetFileName(file));
                if (File.Exists(destFile))
                {
                    continue;
                }
                File.Copy(file, destFile);
            }
            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                if (Directory.Exists(Path.Combine(destDirectory, Path.GetFileName(directory))))
                {
                    continue;
                }
                CopyDirectory(directory, destDirectory);
            }
        }

        public void SetupForPwiz()
        {
            String destDirectory = Path.Combine(FindDirectory("TopographTestProject"), "bin\\x86\\Debug");
            FreshenDirectory(Path.Combine(FindDirectory("turnover_lib"), "bin\\x86\\Debug"), destDirectory);
        }

        protected Workspace CreateWorkspace(string path, DbTracerDef dbTracerDef)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(path, SessionFactoryFlags.CreateSchema))
            {
                using (var session = sessionFactory.OpenSession())
                {
                    var transaction = session.BeginTransaction();
                    var dbWorkspace = new DbWorkspace
                    {
                        ModificationCount = 1,
                        TracerDefCount = dbTracerDef == null ? 0 : 1,
                        SchemaVersion = WorkspaceUpgrader.CurrentVersion,
                    };
                    session.Save(dbWorkspace);
                    if (dbTracerDef != null)
                    {
                        dbTracerDef.Workspace = dbWorkspace;
                        dbTracerDef.Name = "Tracer";
                        session.Save(dbTracerDef);
                    }

                    var modification = new DbModification
                    {
                        DeltaMass = 57.021461,
                        Symbol = "C",
                        Workspace = dbWorkspace
                    };
                    session.Save(modification);
                    transaction.Commit();
                }

            }
            var workspace = new Workspace(path);
            workspace.SetTaskScheduler(TaskScheduler.Default);
            workspace.DatabasePoller.LoadAndMergeChanges(null);
            return workspace;
        }
    }
}
