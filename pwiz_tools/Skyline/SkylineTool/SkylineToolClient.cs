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

namespace SkylineTool
{
    public class SkylineToolClient : IDisposable
    {
        public event EventHandler DocumentChanged; 
        public event SelectionChangedEventHandler SelectionChanged; 
        
        private readonly Client _client;
        private readonly string _toolName;
        private readonly DocumentChangeReceiver _documentChangeReceiver;

        public SkylineToolClient(string connectionName, string toolName)
        {
            _client = new Client(connectionName);
            _toolName = toolName;
            _documentChangeReceiver = new DocumentChangeReceiver(Guid.NewGuid().ToString(), this);
            _documentChangeReceiver.RunAsync();
            _client.AddDocumentChangeReceiver(_documentChangeReceiver.ConnectionName, toolName);
        }

        public void Dispose()
        {
            _client.RemoveDocumentChangeReceiver(_documentChangeReceiver.ConnectionName);
        }

        public IReport GetReport(string reportName)
        {
            var reportCsv = _client.GetReport(_toolName, reportName); // Not L10N
            return new Report(reportCsv);
        }

        public IReport GetReportFromDefinition(string reportDefinition)
        {
            var reportCsv = _client.GetReportFromDefinition(reportDefinition);
            return new Report(reportCsv);
        }

        public DocumentLocation GetDocumentLocation()
        {
            return _client.GetDocumentLocation();
        }

        public void SetDocumentLocation(DocumentLocation documentLocation)
        {
            _client.SetDocumentLocation(documentLocation);
        }

        public string GetDocumentLocationName()
        {
            return _client.GetDocumentLocationName();
        }

        public string GetReplicateName()
        {
            return _client.GetReplicateName();
        }

        public Chromatogram[] GetChromatograms(DocumentLocation documentLocation)
        {
            return _client.GetChromatograms(documentLocation);
        }

        public string GetDocumentPath()
        {
            return _client.GetDocumentPath();
        }

        public Version GetSkylineVersion()
        {
            return _client.GetVersion();
        }

        public void ImportFasta(string textFasta)
        {
            _client.ImportFasta(textFasta);
        }

        public void AddSpectralLibrary(string libraryName, string libraryPath)
        {
            _client.AddSpectralLibrary(libraryName, libraryPath);
        }

        private class DocumentChangeReceiver : RemoteService, IDocumentChangeReceiver
        {
            private readonly SkylineToolClient _toolClient;

            public DocumentChangeReceiver(string connectionName, SkylineToolClient toolClient)
                : base(connectionName)
            {
                _toolClient = toolClient;
            }

            public void DocumentChanged()
            {
                if (_toolClient.DocumentChanged != null)
                    _toolClient.DocumentChanged(_toolClient, null);
            }

            public void SelectionChanged()
            {
                if (_toolClient.SelectionChanged != null)
                    _toolClient.SelectionChanged(_toolClient, null);
            }
        }

        private class Client : RemoteClient, IToolService
        {
            public Client(string connectionName)
                : base(connectionName)
            {
            }

            public string GetReport(string toolName, string reportName)
            {
                return RemoteCallFunction(GetReport, toolName, reportName);
            }

            public string GetReportFromDefinition(string reportDefinition)
            {
                return RemoteCallFunction(GetReportFromDefinition, reportDefinition);
            }

            public DocumentLocation GetDocumentLocation()
            {
                return RemoteCallFunction(GetDocumentLocation);
            }

            public void SetDocumentLocation(DocumentLocation documentLocation)
            {
                RemoteCall(SetDocumentLocation, documentLocation);
            }

            public string GetDocumentLocationName()
            {
                return RemoteCallFunction(GetDocumentLocationName);
            }

            public string GetReplicateName()
            {
                return RemoteCallFunction(GetReplicateName);
            }

            public Chromatogram[] GetChromatograms(DocumentLocation documentLocation)
            {
                return RemoteCallFunction(GetChromatograms, documentLocation);
            }

            public string GetDocumentPath()
            {
                return RemoteCallFunction(GetDocumentPath);
            }

            public Version GetVersion()
            {
                return (Version) RemoteCallFunction((Func<object>) GetVersion);
            }

            public void ImportFasta(string textFasta)
            {
                RemoteCall(ImportFasta, textFasta);
            }

            public void AddSpectralLibrary(string libraryName, string libraryPath)
            {
                RemoteCall(AddSpectralLibrary, libraryName, libraryPath);
            }

            public void AddDocumentChangeReceiver(string receiverName, string name)
            {
                RemoteCall(AddDocumentChangeReceiver, receiverName, name);
            }

            public void RemoveDocumentChangeReceiver(string receiverName)
            {
                RemoteCall(RemoveDocumentChangeReceiver, receiverName);
            }
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
    }

    public interface IReport
    {
        string[] ColumnNames { get; }
        string[][] Cells { get; }
        double?[][] CellValues { get; }
        string Cell(int row, string column);
        double? CellValue(int row, string column);
    }

    public delegate void SelectionChangedEventHandler(object sender, EventArgs args);

}
