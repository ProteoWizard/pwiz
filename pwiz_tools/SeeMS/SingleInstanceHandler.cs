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
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;

/// <summary>
/// A handler for an application to consolidate arguments from multiple instances
/// (within a Timeout period) into a single instance.
/// </summary>
public sealed class SingleInstanceHandler
{
    /// <summary>
    /// Occurs when the Timeout period has elapsed: the single instance is launched with the consolidated argument list.
    /// </summary>
    public event EventHandler<SingleInstanceEventArgs> Launching;

    /// <summary>
    /// Time to wait in milliseconds for additional instances to add their arguments to the first instance's.
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// Constructs a handler for an application to consolidate arguments from multiple instances
    /// (within a Timeout period) into a single instance.
    /// </summary>
    /// <param name="uniqueID">A unique string for the application.</param>
    public SingleInstanceHandler (string uniqueID)
    {
        var rng = new Random(uniqueID.GetHashCode());
        byte[] ipcMutexGuidBytes = new byte[16];
        byte[] ipcNamedPipeGuidBytes = new byte[16];
        rng.NextBytes(ipcMutexGuidBytes);
        rng.NextBytes(ipcNamedPipeGuidBytes);
        ipcMutexGuid = new Guid(ipcMutexGuidBytes).ToString().Trim('{', '}');
        ipcNamedPipeGuid = new Guid(ipcNamedPipeGuidBytes).ToString().Trim('{', '}');

        Timeout = 500;
    }

    /// <summary>
    /// Launch a new instance using 'args' or consolidate 'args' into a recent instance. Returns exit code of the Launching event if new instance was launched.
    /// </summary>
    public int Connect (string[] args)
    {
        if (Launching == null)
            return 1; // nothing to do

        // create global named mutex
        using (ipcMutex = new Mutex(false, ipcMutexGuid))
        {
            // if the global mutex is not locked, wait for args from additional instances
            if (ipcMutex.WaitOne(0))
                return waitForAdditionalInstances(args);
            else
                return sendArgsToExistingInstance(args);
        }
    }

    private int waitForAdditionalInstances (string[] args)
    {
        var accumulatedArgs = new List<string>(args);

        while (true)
        {
            var signal = new ManualResetEvent(false);
            using (var pipeServer = new NamedPipeServerStream(ipcNamedPipeGuid, PipeDirection.In, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                pipeServer.BeginWaitForConnection(x =>
                {
                    // if timed out, stop waiting for a connection
                    if (signal.WaitOne(0))
                    {
                        signal.Close();
                        return;
                    }

                    pipeServer.EndWaitForConnection(x);
                    signal.Set();
                }, null);

                // no client connected to the pipe within the Timeout period
                if (!signal.WaitOne(Timeout, true))
                {
                    signal.Set();
                    break;
                }

                using (var sr = new StreamReader(pipeServer))
                {
                    int length = Convert.ToInt32(sr.ReadLine());
                    for (int i = 0; i < length; ++i)
                        accumulatedArgs.Add(sr.ReadLine());
                }
            }

            // new args have been added to accumulatedArgs, continue loop to listen for another client
        }

        ipcMutex.Close();
        var eventArgs = new SingleInstanceEventArgs(accumulatedArgs.ToArray());
        Launching?.Invoke(this, eventArgs);
        return eventArgs.ExitCode;
    }

    private int sendArgsToExistingInstance (string[] args)
    {
        var pipeClient = new NamedPipeClientStream(".", ipcNamedPipeGuid, PipeDirection.Out);

        // try to connect to the pipe server for the Timeout period
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(args.Length.ToString());
            foreach (string arg in args)
                sb.AppendLine(arg);

            byte[] buffer = Encoding.ASCII.GetBytes(sb.ToString());

            pipeClient.Connect(Timeout);

            // can this ever happen? if it does, don't handle it like a timeout exception
            if (!pipeClient.IsConnected)
                throw new Exception("did not throw exception");

            pipeClient.Write(buffer, 0, buffer.Length);
            return 0;
        }
        catch (Exception e)
        {
            if (!e.Message.ToLower().Contains("time"))
                throw;

            // no server was running; launch a new instance
            var eventArgs = new SingleInstanceEventArgs(args);
            Launching?.Invoke(this, eventArgs);
            return eventArgs.ExitCode;
        }
    }

    private string ipcMutexGuid;
    private string ipcNamedPipeGuid;
    private Mutex ipcMutex;
}

/// <summary>
/// Stores the consolidated argument list from one or more instances of an application.
/// </summary>
public sealed class SingleInstanceEventArgs : EventArgs
{
    public SingleInstanceEventArgs (string[] args) { Args = args; }

    /// <summary>
    /// The consolidated argument list from one or more instances of an application.
    /// </summary>
    public string[] Args { get; private set; }

    /// <summary>
    /// A return code from the launched instance.
    /// </summary>
    public int ExitCode { get; set; }
}