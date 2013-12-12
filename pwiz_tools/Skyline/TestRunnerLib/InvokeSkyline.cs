using System;
using System.IO;
using System.Reflection;

namespace TestRunnerLib
{
    public class InvokeSkyline
    {
        private readonly Type _skylineProgram;

        public InvokeSkyline()
        {
            var skylinePath = GetAssemblyPath("Skyline-daily.exe");
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
            _skylineProgram.GetMethod(method).Invoke(null, args);
        }

        public void Set(string field, object value)
        {
            _skylineProgram.GetMethod("set_" + field).Invoke(null, new[] {value});
        }

        public T Get<T>(string field)
        {
            return (T) _skylineProgram.GetMethod("get_" + field).Invoke(null, null);
        }
    }
}
