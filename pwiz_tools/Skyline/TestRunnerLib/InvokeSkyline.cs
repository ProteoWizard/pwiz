using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace TestRunnerLib
{
    public class InvokeSkyline
    {
        private readonly Type _skylineProgram;

        public InvokeSkyline()
        {
            var skylinePath = GetAssemblyPath("Skyline-daily.exe"); // Keep -daily
            if (!File.Exists(skylinePath))
                skylinePath = GetAssemblyPath("Skyline.exe");
            var skylineAssembly = Assembly.LoadFrom(skylinePath);
            _skylineProgram = skylineAssembly.GetType("pwiz.Skyline.Program");
        }

        private static string GetAssemblyPath(string assembly)
        {
            var runnerExeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (runnerExeDirectory == null) 
                throw new ApplicationException("Can't find path to assembly");
            return Path.Combine(runnerExeDirectory, assembly);
        }

        public void Run(string method, params object[] args)
        {
            CheckSettings();

            // ReSharper disable once PossibleNullReferenceException
            _skylineProgram.GetMethod(method).Invoke(null, args);
        }

        public void Set(string field, object value)
        {
            // ReSharper disable once PossibleNullReferenceException
            _skylineProgram.GetMethod("set_" + field).Invoke(null, new[] {value});
        }

        public T Get<T>(string field)
        {
            // ReSharper disable once PossibleNullReferenceException
            return (T) _skylineProgram.GetMethod("get_" + field).Invoke(null, null);
        }

        public void CheckSettings()
        {
            try
            {
                Get<string>("Name");
            }
            catch (Exception getNameException)
            {
                // ReSharper disable LocalizableElement
                StringBuilder message = new StringBuilder();
                message.AppendLine("Error initializing settings");
                var exeConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.None);
                message.AppendLine("Exe Config:" + exeConfig.FilePath);
                var localConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
                message.AppendLine("Local Config:" + localConfig.FilePath);
                var roamingConfig =
                    System.Configuration.ConfigurationManager.OpenExeConfiguration(
                        System.Configuration.ConfigurationUserLevel.PerUserRoaming);
                message.AppendLine("Roaming Config:" + roamingConfig.FilePath);
                throw new Exception(message.ToString(), getNameException);
                // ReSharper restore LocalizableElement
            }
        }
    }
}
