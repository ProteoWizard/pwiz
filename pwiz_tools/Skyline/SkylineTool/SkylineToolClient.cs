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
using System.ServiceModel;

namespace SkylineTool
{
    /// <summary>
    /// Client object created by an interactive tool to communicate with
    /// the tool service in the Skyline process that started the tool.
    /// </summary>
    public class SkylineToolClient : IDisposable
    {
        /// <summary>
        /// Event for notification of document changes in Skyline.
        /// </summary>
        public event EventHandler DocumentChanged;

        private readonly string _toolName;
        private readonly Client _client;

        /// <summary>
        /// Create the client object to communicate with Skyline. The connectionName
        /// is normally passed to the tool from Skyline as a command line argument,
        /// named $(SkylineConnection) in tool macros.
        /// </summary>
        /// <param name="toolName">Name of Skyline tool.</param>
        /// <param name="connectionName">Name of connection to Skyline.</param>
        public SkylineToolClient(string toolName, string connectionName)
        {
            _toolName = toolName;
            _client = new Client(this, connectionName);
        }

        public void Dispose()
        {
            _client.Close();
        }
        
        public void Select(string link)
        {
            _client.Select(link);
        }

        public string DocumentPath
        {
            get { return _client.DocumentPath; }
        }

        public Version SkylineVersion
        {
            get { return _client.Version; }
        }

        public IReport GetReport(string reportName)
        {
            return new Report(_client.GetReport(_toolName, reportName));
        }

        public int RunTest(string testName)
        {
            return _client.RunTest(testName);
        }

        private class Report : IReport
        {
            public Report(string reportCsv)
            {
                var lines = reportCsv.Split(new [] {"\r\n"}, StringSplitOptions.None); // Not L10N
                ColumnNames = lines[0].Split(',');
                Cells = new string[lines.Length-1][];
                CellValues = new double?[lines.Length-1][];
                for (int i = 0; i < lines.Length-1; i++)
                {
                    Cells[i] = new string[ColumnNames.Length];
                    CellValues[i] = new double?[ColumnNames.Length];
                    var row = lines[i + 1].Split(',');
                    for (int j = 0; j < row.Length; j++)
                    {
                        Cells[i][j] = row[j];
                        double value;
                        if (double.TryParse(row[j], out value))
                            CellValues[i][j] = value;
                    }
                }
            }

            public string[] ColumnNames { get; private set; }
            public string[][] Cells { get; private set; }
            public double?[][] CellValues { get; private set; }
            
            public string Cell(int row, string columnName)
            {
                int column = FindColumn(columnName);
                return column >= 0 ? Cells[row][column] : null;
            }

            public double? CellValue(int row, string columnName)
            {
                int column = FindColumn(columnName);
                return column >= 0 ? CellValues[row][column] : null;
            }

            private int FindColumn(string columnName)
            {
                for (int i = 0; i < ColumnNames.Length; i++)
                {
                    if (string.Equals(columnName, ColumnNames[i], StringComparison.InvariantCultureIgnoreCase))
                        return i;
                }
                return -1;
            }
        }

        private class Client : ISkylineTool, ISkylineToolEvents
        {
            private const int MaxMessageLength = int.MaxValue;

            private readonly DuplexChannelFactory<ISkylineTool> _channelFactory;
            private readonly ISkylineTool _channel;
            private readonly SkylineToolClient _parent;
            private System.Timers.Timer _timer;

            public Client(SkylineToolClient parent, string connectionName)
            {
                _parent = parent;
                var binding = new NetNamedPipeBinding
                {
                    MaxReceivedMessageSize = MaxMessageLength,
                    ReceiveTimeout = TimeSpan.MaxValue,
                    SendTimeout = TimeSpan.MaxValue
                };
                _channelFactory = new DuplexChannelFactory<ISkylineTool>(
                    new InstanceContext(this),
                    binding,
                    new EndpointAddress(SkylineToolService.GetAddress(connectionName)));
                _channel = _channelFactory.CreateChannel();
                NotifyDocumentChanged();
            }

            public void Close()
            {
                _channelFactory.Close();
            }

            public void DocumentChangedEvent()
            {
                if (_parent.DocumentChanged != null  && _timer == null)
                {
                    // We must execute the document change handler after the DocumentChanged event
                    // returns, otherwise the tool service will deadlock.
                    _timer = new System.Timers.Timer(100);
                    _timer.Elapsed += DelayedDocumentChange;
                    _timer.Start();
                }
            }

            private void DelayedDocumentChange(object sender, System.Timers.ElapsedEventArgs e)
            {
                _timer.Elapsed -= DelayedDocumentChange;
                _timer.Stop();
                _timer = null;

                if (_parent.DocumentChanged != null)
                    _parent.DocumentChanged(_parent, null);
            }


            public string GetReport(string toolName, string reportName)
            {
                return _channel.GetReport(toolName, reportName);
            }

            public void Select(string link)
            {
                _channel.Select(link);
            }

            public string DocumentPath
            {
                get { return _channel.DocumentPath; }
            }

            public Version Version
            {
                get { return _channel.Version; }
            }

            public void NotifyDocumentChanged()
            {
                _channel.NotifyDocumentChanged();
            }

            public int RunTest(string testName)
            {
                return _channel.RunTest(testName);
            }
        }
    }

    public interface IReport
    {
        string[] ColumnNames { get; }
        string[][] Cells { get; }
        double?[][] CellValues { get; }
        string Cell(int row, string column);
        double? CellValue(int row, string column);
    }
}
