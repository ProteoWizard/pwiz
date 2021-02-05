using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

namespace SharedAutoQcBatch
{
    public class ProgramLog
    {

        private static ILog Log;

        private static void Init()
        {
            if (Log == null)
            {
                Log = LogManager.GetLogger(Program.LOG_NAME);
            }
        }

        public static void LogError(string message)
        {
            Init();
            Log.Error(message);
        }

        public static void LogError(string configName, string message)
        {
            Init();
            Log.Error(string.Format("{0}: {1}", configName, message));
        }

        public static void LogError(string message, Exception e)
        {
            Init();
            Log.Error(message, e);
        }

        public static void LogError(string configName, string message, Exception e)
        {
            Init();
            LogError(string.Format("{0}: {1}", configName, message), e);
        }

        public static void LogInfo(string message)
        {
            Init();
            Log.Info(message);
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
