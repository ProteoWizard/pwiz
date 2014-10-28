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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SkylineTool
{
    public class RemoteService : IRemotable
    {
        private readonly List<Channel> _channels = new List<Channel>();
        private bool _quit;
        private readonly CountdownEvent _quitCountDown;

        /// <summary>
        /// Create remote service that communicates with a client.
        /// </summary>
        /// <param name="connectionName">Channel name (used to match with the client).</param>
        protected RemoteService(string connectionName)
        {
            ConnectionName = connectionName;

            // Create a service thread for each interface method.
            foreach (var anInterface in GetType().FindInterfaces((type, criteria) => true, string.Empty))
            {
                if (anInterface.Name == "IDisposable") // Not L10N
                    continue;
                var interfaceMap = GetType().GetInterfaceMap(anInterface);
                foreach (var method in interfaceMap.TargetMethods)
                {
                    if (method.IsPublic)
                    {
                        // Start service thread.
                        var worker = new BackgroundWorker();
                        worker.DoWork += ServiceThread;
                        var channel = new Channel(ConnectionName, this, method);
                        worker.RunWorkerAsync(channel);
                        _channels.Add(channel);
                    }
                }
            } 

            // Counter to wait for all threads to exit.
            _quitCountDown = new CountdownEvent(_channels.Count);
        }

        public void Dispose()
        {
            if (!_quit)
            {
                Quit();
                WaitForExit();
            }
            foreach (var channel in _channels)
                channel.Dispose();
            _quitCountDown.Dispose();
        }

        public string ConnectionName { get; private set; }

        public string Quit()
        {
            _quit = true;
            foreach (var channel in _channels)
                channel.ReleaseServer();
            return Process.GetCurrentProcess().Id.ToString("D"); // Not L10N
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

            try
            {
                // Name the thread for easier identification in the debugger.
                Thread.CurrentThread.Name = "*" + channel.Name; // Not L10N

                while (!_quit)
                {
                    // Wait for a message from the client.
                    channel.WaitServer();

                    if (_quit)
                        break;

                    // Create a shared file for communication.
                    using (var sharedFile = new SharedFile(channel.SharedFileName))
                    {
                        object result;
                        if (channel.HasArg)
                        {
                            // Read data from shared file.
                            string data;
                            using (var stream = sharedFile.CreateViewStream())
                            {
                                using (var reader = new BinaryReader(stream))
                                {
                                    data = reader.ReadString();
                                }
                            }

                            // Call service method.
                            result = channel.RunMethod(data);
                        }
                        else
                        {
                            result = channel.RunMethod();
                        }

                        if (channel.HasReturn)
                        {
                            // Write result to shared file.
                            using (var stream = sharedFile.CreateViewStream())
                            {
                                using (var writer = new BinaryWriter(stream))
                                {
                                    if (result == null)
                                        writer.Write(string.Empty);
                                    else
                                    {
                                        var s = result as string;
                                        if (s != null)
                                            writer.Write(s);
                                        else
                                        {
                                            var chromatograms = (IChromatogram[]) result;
                                            writer.Write(chromatograms.Length);
                                            foreach (var chromatogram in chromatograms)
                                                chromatogram.Write(writer);
                                        }
                                    }
                                }
                            }
                        }

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
