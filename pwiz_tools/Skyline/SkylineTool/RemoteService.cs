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
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SkylineTool
{
    public class RemoteService : RemoteBase, IDisposable
    {
        private readonly List<Channel> _channels = new List<Channel>();
        private bool _exit;
        private readonly CountdownEvent _quitCountDown;

        /// <summary>
        /// Create remote service that communicates with a client.
        /// </summary>
        /// <param name="connectionName">Channel name (used to match with the client).</param>
        protected RemoteService(string connectionName)
        {
            ConnectionName = connectionName;

            // Create a service thread for each interface method.
            foreach (var pair in MethodInfos)
            {
                // Start service thread.
                var worker = new BackgroundWorker();
                worker.DoWork += ServiceThread;
                var channel = new Channel(ConnectionName, this, pair.Value);
                worker.RunWorkerAsync(channel);
                _channels.Add(channel);
            }

            // Counter to wait for all threads to exit.
            _quitCountDown = new CountdownEvent(_channels.Count);
        }

        public void Dispose()
        {
            if (!_exit)
            {
                Exit();
                WaitForExit();
            }
            foreach (var channel in _channels)
                channel.Dispose();
            _quitCountDown.Dispose();
        }

        public string ConnectionName { get; private set; }

        protected void Exit()
        {
            _exit = true;
            foreach (var channel in _channels)
                channel.ReleaseServer();
        }

        /// <summary>
        /// Wait for all service threads to exit.
        /// </summary>
        public void WaitForExit()
        {
            _quitCountDown.Wait();
        }

        private void ServiceThread(object sender, DoWorkEventArgs e)
        {
            var channel = (Channel) e.Argument;

            // Name the thread for easier identification in the debugger.
            Thread.CurrentThread.Name = "*" + channel.Name; // Not L10N

            try
            {
                while (!_exit)
                {
                    // Wait for a message from the client.
                    channel.WaitServer();

                    if (_exit)
                        break;

                    // Create a shared file for communication.
                    using (var sharedFile = channel.Open())
                    {
                        // Read data from shared file.
                        string data = channel.HasArg ? sharedFile.Read() : null;

                        // Call service method.
                        object result = channel.RunMethod(data);

                        // Write result to shared file.
                        if (channel.HasReturn)
                            sharedFile.Write(result);

                        // Signal client that the result is ready.
                        channel.ReleaseClient();
                    }
                }

                _quitCountDown.Signal();
            }
            catch (Exception ex)
            {
                Console.WriteLine("EXCEPTION ({0}): {1}", channel.Name, ex); // Not L10N
                throw;
            }
        }
    }
}
