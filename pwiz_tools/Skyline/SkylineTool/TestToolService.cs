/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Diagnostics;

namespace SkylineTool
{
    public interface ITestTool
    {
        // Cross-process communication tests.
        float TestFloat(float data);
        float[] TestFloatArray();
        string TestString();
        string[] TestStringArray();
        Version[] TestVersionArray();
        Chromatogram[] TestChromatogramArray();
        void ImportFasta(string fasta);
        void TestAddSpectralLibrary(string libraryName, string libraryPath);

        void TestSelect(string link);
        void TestSelectReplicate(string link);
        Version TestVersion();
        string TestDocumentPath();
        int GetDocumentChangeCount();
        int Quit();
    }

    public class TestToolClient : RemoteClient, ITestTool
    {
        public TestToolClient(string connectionName)
            : base(connectionName)
        {
        }

        public void Exit()
        {
            var processId = Quit();
            var process = Process.GetProcessById(processId);
            process.Kill();
            try
            {
                process.WaitForExit();
            }
// ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
        }

        public float TestFloat(float data)
        {
            return RemoteCallFunction(TestFloat, data);
        }

        public float[] TestFloatArray()
        {
            return RemoteCallFunction(TestFloatArray);
        }

        public string TestString()
        {
            return RemoteCallFunction(TestString);
        }

        public string[] TestStringArray()
        {
            return RemoteCallFunction(TestStringArray);
        }

        public Version[] TestVersionArray()
        {
            return RemoteCallFunction(TestVersionArray);
        }

        public Chromatogram[] TestChromatogramArray()
        {
            return RemoteCallFunction(TestChromatogramArray);
        }

        public void ImportFasta(string textFasta)
        {
            RemoteCall(ImportFasta, textFasta);
        }

        public void TestAddSpectralLibrary(string libraryName, string libraryPath)
        {
            RemoteCall(TestAddSpectralLibrary, libraryName, libraryPath);
        }

        public void TestSelect(string link)
        {
            RemoteCall(TestSelect, link);
        }

        public void TestSelectReplicate(string link)
        {
            RemoteCall(TestSelectReplicate, link);
        }

        public Version TestVersion()
        {
            return RemoteCallFunction(TestVersion);
        }

        public string TestDocumentPath()
        {
            return RemoteCallFunction(TestDocumentPath);
        }

        public int GetDocumentChangeCount()
        {
            return RemoteCallFunction(GetDocumentChangeCount);
        }

        public int Quit()
        {
            return RemoteCallFunction(Quit);
        }
    }
}
