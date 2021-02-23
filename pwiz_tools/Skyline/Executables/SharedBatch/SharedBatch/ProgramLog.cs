using System;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

namespace SharedBatch
{
    public class ProgramLog
    {

        private static ILog Log;

        public static void Init(string logName)
        {
            if (Log == null) Log = LogManager.GetLogger(logName);
        }

        public static void Error(string message)
        {
            Log?.Error(message);
        }

        public static void Error(string configName, string message)
        {
            Log?.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void Error(string message, Exception e)
        {
            Log?.Error(message, e);
        }

        public static void Error(string configName, string message, Exception e)
        {
            Error(string.Format("{0}: {1}", configName, message), e);
        }

        public static void Info(string message)
        {
            Log?.Info(message);
        }

        public static string GetProgramLogFilePath()
        {
            var repository = ((Hierarchy)LogManager.GetRepository());
            FileAppender rootAppender = null;
            if (repository != null)
            {
                rootAppender = repository.Root.Appenders.OfType<FileAppender>()
                    .FirstOrDefault();
            }
            return rootAppender != null ? rootAppender.File : string.Empty;
        }
    }
}
