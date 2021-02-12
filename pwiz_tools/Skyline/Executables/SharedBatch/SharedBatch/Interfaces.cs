using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace SharedBatch
{

    public delegate IConfigRunner CreateRunner(IConfig config, Logger logger, IMainUiControl uiControl = null);
    public interface IConfig
    {
        void Validate();

        string GetName();

        DateTime GetModified();

        bool TryPathReplace(string oldRoot, string newRoot, out IConfig replaced);

        void WriteXml(XmlWriter writer);

        ListViewItem AsListViewItem(IConfigRunner runner);
    }


    public enum RunnerStatus
    {
        Waiting,
        Starting,
        Running,
        Cancelling,
        Cancelled,
        Stopping,
        Stopped,
        Completed,
        Disconnected,
        Error
    }
    public interface IConfigRunner
    {
        string GetConfigName();

        IConfig GetConfig();

        RunnerStatus GetStatus();

        string GetDisplayStatus();

        Color GetDisplayColor();

        bool IsBusy();
        bool IsRunning();
        void Cancel();
    }

    /*public interface ILogger
    {
        void Log(string message, params object[] args);
        /*void LogError(string message, params object[] args);
        void LogProgramError(string message, params object[] args);
        void LogException(Exception exception, string message, params object[] args);* /
        string GetFile();
        string GetFileName();
        void Delete();
        //ILogger Archive();


        void DisplayLog();
    }*/

    public enum ConfigAction
    {
        Add, Edit, Copy
    }


    public interface IMainUiControl
    {
        void TryExecuteOperation(ConfigAction action, IConfig config);
        //void EditSelectedConfiguration(IConfig newVersion);
        void UpdateUiConfigurations();
        void UpdateUiLogFiles();
        void UpdateRunningButtons(bool canStart, bool canStop);

        void LogToUi(string filePath, string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string filePath, string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(string filePath, List<string> lines);
        void LogErrorLinesToUi(string filePath, List<string> lines);

        void DisplayError(string message);
        void DisplayWarning(string message);
        void DisplayInfo(string message);
        void DisplayErrorWithException(string message, Exception exception);
        DialogResult DisplayQuestion(string message);
        DialogResult DisplayLargeQuestion(string message);
    }
}
