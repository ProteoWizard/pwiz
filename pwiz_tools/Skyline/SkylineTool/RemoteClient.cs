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

using System.IO;

namespace SkylineTool
{
    public class RemoteClient : IRemotable
    {
        protected RemoteClient(string connectionName)
        {
            ConnectionName = connectionName;
        }

        public void Dispose()
        {
        }

        public string ConnectionName { get; private set; }

        /// <summary>
        /// Request the server to shutdown.
        /// </summary>
        public string Quit()
        {
            return RemoteCall("Quit", true); // Not L10N
        }

        /// <summary>
        /// Make a remote call to the server.
        /// </summary>
        /// <param name="methodName">Name of method to execute on the server.</param>
        /// <param name="hasReturn">True if method returns a value.</param>
        /// <param name="data">Data to pass to the server method.</param>
        /// <returns>Result string from server method.</returns>
        protected string RemoteCall(string methodName, bool hasReturn = false, string data = null)
        {
            string result = null;

            // Create channel for communicating with the server.
            using (var channel = new Channel(ConnectionName, methodName))
            {
                using (var sharedFile = new SharedFile(channel.SharedFileName))
                {
                    if (data != null)
                    {
                        // Write data to shared file.
                        using (var stream = sharedFile.CreateViewStream())
                        {
                            using (var writer = new BinaryWriter(stream))
                            {
                                writer.Write(data);
                            }
                        }
                    }

                    // Notify server that it can process the data.
                    channel.ReleaseServer();

                    // Wait for server to finish.
                    channel.WaitClient();

                    // Read result from shared file.
                    if (hasReturn)
                    {
                        using (var stream = sharedFile.CreateViewStream())
                        {
                            using (var reader = new BinaryReader(stream))
                            {
                                result = reader.ReadString();
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
