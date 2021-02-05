using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;

namespace SharedAutoQcBatch
{

    public delegate IConfigRunner CreateRunner(IConfig config, Logger logger, IMainUiControl uiControl = null);
    public interface IConfig
    {
        void Validate();

        string GetName();

        DateTime GetModified();

        CreateRunner GetRunnerCreator();

        bool TryPathReplace(string oldRoot, string newRoot, out IConfig replaced);

        void WriteXml(XmlWriter writer);

        ListViewItem AsListViewItem(IConfigRunner runner);
    }


    public enum RunnerStatus
    {
        Waiting,
        Running,
        Cancelling,
        Cancelled,
        Stopped,
        Completed,
        Error
    }
    public interface IConfigRunner
    {
        string GetConfigName();

        IConfig GetConfig();

        RunnerStatus GetStatus();

        bool IsBusy();
        bool IsRunning();
        bool IsWaiting();
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

    public interface IMainUiControl
    {
        void AddConfiguration(IConfig config);
        void EditSelectedConfiguration(IConfig newVersion);
        void UpdateUiConfigurations();

        void UpdateUiLogFiles();
        void UpdateRunningButtons(bool isRunning);

        void LogToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogErrorToUi(string text, bool scrollToEnd = true, bool trim = true);
        void LogLinesToUi(List<string> lines);
        void LogErrorLinesToUi(List<string> lines);

        void DisplayError(string message);
        void DisplayWarning(string message);
        void DisplayInfo(string message);
        void DisplayErrorWithException(string message, Exception exception);
        DialogResult DisplayQuestion(string message);
        DialogResult DisplayLargeQuestion(string message);
    }
}
