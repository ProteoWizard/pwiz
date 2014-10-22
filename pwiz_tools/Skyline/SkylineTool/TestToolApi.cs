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

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace SkylineTool
{
    public class TestToolApi : IDisposable
    {
        private readonly Semaphore _skylineSemaphore;
        private readonly Semaphore _toolSemaphore;
        private readonly MemoryMappedFile _sharedFile;
        private const int MessageSize = 10000;

        public TestToolApi(string name, bool server = false)
        {
            bool createdNew;
            _skylineSemaphore = new Semaphore(0, 1, name + "-Skyline", out createdNew); // Not L10N
            Assert(createdNew == server);
            _toolSemaphore = new Semaphore(0, 1, name + "-Tool", out createdNew); // Not L10N
            Assert(createdNew == server);
            _sharedFile = MemoryMappedFile.CreateOrOpen(name + "-File", MessageSize); // Not L10N
        }

        public void Dispose()
        {
            _skylineSemaphore.Dispose();
            _toolSemaphore.Dispose();
            _sharedFile.Dispose();
        }

        private void Assert(bool condition)
        {
            if (!condition)
                throw new NotSupportedException();
        }

        public string RunTool(string message)
        {
            WriteMessage(message);
            _toolSemaphore.Release();
            _skylineSemaphore.WaitOne();
            return ReadMessage();
        }

        public string GetMessage()
        {
            _toolSemaphore.WaitOne();
            return ReadMessage();
        }

        public void ReturnMessage(string message)
        {
            WriteMessage(message);
            _skylineSemaphore.Release();
        }

        private void WriteMessage(string message)
        {
            using (var stream = _sharedFile.CreateViewStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write(message);
            }
        }

        private string ReadMessage()
        {
            using (var stream = _sharedFile.CreateViewStream())
            {
                var reader = new BinaryReader(stream);
                return reader.ReadString();
            }
        }
    }
}
