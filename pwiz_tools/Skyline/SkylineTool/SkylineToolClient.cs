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
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable All

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
            var reportCsv = _client.GetReport(_toolName, reportName);
            return new Report(reportCsv);
        }

        public IReport GetReportFromDefinition(string reportDefinition)
        {
            var reportCsv = _client.GetReportFromDefinition(reportDefinition);
            return new Report(reportCsv);
        }

        [Obsolete]
        public DocumentLocation GetDocumentLocation()
        {
            return _client.GetDocumentLocation();
        }

        [Obsolete]
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

        [Obsolete]
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

        public void InsertSmallMoleculeTransitionList(string textCSV)
        {
            _client.InsertSmallMoleculeTransitionList(textCSV);
        }

        public void AddSpectralLibrary(string libraryName, string libraryPath)
        {
            _client.AddSpectralLibrary(libraryName, libraryPath);
        }

        public int GetProcessId()
        {
            return _client.GetProcessId();
        }

        public void DeleteElements(string[] elementLocators)
        {
            _client.DeleteElements(elementLocators);
        }

        public string GetSelectedElementLocator(string elementType)
        {
            return _client.GetSelectedElementLocator(elementType);
        }

        public void ImportProperties(string propertiesCsv)
        {
            _client.ImportProperties(propertiesCsv);
        }

        public void ImportPeakBoundaries(string peakBoundariesCsv)
        {
            _client.ImportPeakBoundaries(peakBoundariesCsv);
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

        private class Client : IToolService
        {
            private readonly RemoteClient _remoteClient;
            public Client(string connectionName)
            {
                _remoteClient = new RemoteClient(connectionName);
            }

            public string GetReport(string toolName, string reportName)
            {
                return _remoteClient.RemoteCallFunction(GetReport, toolName, reportName);
            }

            public string GetReportFromDefinition(string reportDefinition)
            {
                return _remoteClient.RemoteCallFunction(GetReportFromDefinition, reportDefinition);
            }

            [Obsolete]
            public DocumentLocation GetDocumentLocation()
            {
                return _remoteClient.RemoteCallFunction(GetDocumentLocation);
            }

            [Obsolete]
            public void SetDocumentLocation(DocumentLocation documentLocation)
            {
                _remoteClient.RemoteCall(SetDocumentLocation, documentLocation);
            }

            public string GetDocumentLocationName()
            {
                return _remoteClient.RemoteCallFunction(GetDocumentLocationName);
            }

            public string GetReplicateName()
            {
                return _remoteClient.RemoteCallFunction(GetReplicateName);
            }

            [Obsolete]
            public Chromatogram[] GetChromatograms(DocumentLocation documentLocation)
            {
                return _remoteClient.RemoteCallFunction(GetChromatograms, documentLocation);
            }

            public string GetDocumentPath()
            {
                return _remoteClient.RemoteCallFunction(GetDocumentPath);
            }

            public Version GetVersion()
            {
                return (Version) _remoteClient.RemoteCallFunction((Func<object>) GetVersion);
            }

            public void ImportFasta(string textFasta)
            {
                _remoteClient.RemoteCall(ImportFasta, textFasta);
            }

            public void InsertSmallMoleculeTransitionList(string textCSV)
            {
                _remoteClient.RemoteCall(InsertSmallMoleculeTransitionList, textCSV);
            }

            public void AddSpectralLibrary(string libraryName, string libraryPath)
            {
                _remoteClient.RemoteCall(AddSpectralLibrary, libraryName, libraryPath);
            }

            public void AddDocumentChangeReceiver(string receiverName, string name)
            {
                _remoteClient.RemoteCall(AddDocumentChangeReceiver, receiverName, name);
            }

            public void RemoveDocumentChangeReceiver(string receiverName)
            {
                _remoteClient.RemoteCall(RemoveDocumentChangeReceiver, receiverName);
            }

            public int GetProcessId()
            {
                return _remoteClient.RemoteCallFunction(GetProcessId);
            }

            public void DeleteElements(string[] elementLocators)
            {
                _remoteClient.RemoteCall(DeleteElements, elementLocators);
            }

            public void ImportProperties(string csvText)
            {
                _remoteClient.RemoteCall(ImportProperties, csvText);
            }

            public void ImportPeakBoundaries(string csvText)
            {
                _remoteClient.RemoteCall(ImportPeakBoundaries, csvText);
            }

            public string GetSelectedElementLocator(string elementType)
            {
                return _remoteClient.RemoteCallFunction(GetSelectedElementLocator, elementType);
            }
        }

        private class Report : IReport
        {
            private double?[][] _cellValues;
            public Report(string reportCsv)
            {
                const char sep = ',';
                TextReader reader = new StringReader(reportCsv);
                ColumnNames = ReadDsvLine(reader, sep).ToArray();
                var rows = new List<List<string>>();
                List<string> line;
                while (null != (line = ReadDsvLine(reader, sep)))
                {
                    rows.Add(line);
                }
                Cells = rows.Select(row => row.ToArray()).ToArray();
            }

            public string[] ColumnNames { get; private set; }
            public string[][] Cells { get; private set; }

            public double?[][] CellValues
            {
                get
                {
                    if (_cellValues == null)
                    {
                        _cellValues = Cells.Select(row => row.Select(cell =>
                        {
                            if (double.TryParse(cell, out double value))
                            {
                                return value;
                            }

                            return (double?) null;
                        }).ToArray()).ToArray();
                    }

                    return _cellValues;
                }
            }

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
        private static List<string> ReadDsvLine(TextReader reader, char separator)
        {
            List<string> fields = new List<string>();
            StringBuilder currentValue = null;
            bool inQuote = false;
            while (true)
            {
                int nextValue = reader.Read();
                if (nextValue == -1)
                {
                    if (currentValue == null)
                    {
                        return null;
                    }
                    fields.Add(currentValue.ToString());
                    return fields;
                }

                char chNext = (char)nextValue;

                currentValue = currentValue ?? new StringBuilder();
                if (inQuote)
                {
                    if (chNext == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            currentValue.Append('"');
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        currentValue.Append(chNext);
                    }
                }
                else
                {
                    if (chNext == separator)
                    {
                        fields.Add(currentValue.ToString());
                        currentValue.Clear();
                    }
                    else if (chNext == '"')
                    {
                        inQuote = true;
                    }
                    else if (chNext == '\r' || chNext == '\n')
                    {
                        if (chNext == '\r' && reader.Peek() == '\n')
                        {
                            reader.Read();
                        }
                        fields.Add(currentValue.ToString());
                        return fields;
                    }
                    else
                    {
                        currentValue.Append(chNext);
                    }
                }
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
