using System;
using SkylineTool;

namespace ToolServiceCmd
{
    public class ToolServiceClient : RemoteClient, IToolService, IDisposable
    {
        public ToolServiceClient(string connectionName)
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

        public SkylineTool.Version GetVersion()
        {
            return (SkylineTool.Version)RemoteCallFunction((Func<object>)GetVersion);
        }

        public void ImportFasta(string textFasta)
        {
            RemoteCall(ImportFasta, textFasta);
        }

        public void InsertSmallMoleculeTransitionList(string textCSV)
        {
            RemoteCall(InsertSmallMoleculeTransitionList, textCSV);
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

        public void Dispose()
        {
            
        }

        public int GetProcessId()
        {
            return RemoteCallFunction(GetProcessId);
        }

        public void DeleteElements(string[] elementLocators)
        {
            RemoteCall(DeleteElements, elementLocators);
        }

        public void ImportProperties(string csvText)
        {
            RemoteCall(ImportProperties, csvText);
        }

        public void ImportPeakBoundaries(string csvText)
        {
            RemoteCall(ImportPeakBoundaries, csvText);
        }

        public string GetSelectedElementLocator(string elementType)
        {
            return RemoteCallFunction(GetSelectedElementLocator, elementType);
        }
    }
}
