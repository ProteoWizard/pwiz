using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TestRunnerLib
{

    public class LoadFromAssembly
    {
        // Offer a more detailed error message when we fail to load a DLL
        public static Assembly Try(string dllPath)
        {
            return Load(dllPath, "Assembly.LoadFrom", Assembly.LoadFrom);
        }

        /// <summary>
        /// Loads an assembly from its bytes rather than from the file, so the file is NOT held
        /// for the life of the process the way <see cref="Assembly.LoadFrom(string)"/> holds it.
        /// <para>Use this to READ a build directory that something else still has to WRITE.
        /// SkylineTester fills its test tree from the staged directory and then stages into that
        /// same directory when a run starts, so the tree it had just shown was what stopped the
        /// staging the run depends on: "The process cannot access the file ...\CommonTest.dll
        /// because it is being used by another process. Held by: SkylineTester".</para>
        /// <para>The trade is that this drops LoadFrom's probing: a dependency found only next to
        /// the assembly no longer resolves by itself, so a caller reading from a directory that is
        /// not its own supplies those. Type identity is unaffected for reading names and
        /// attributes, which is all a test tree needs.</para>
        /// </summary>
        public static Assembly TryWithoutLocking(string dllPath)
        {
            return Load(dllPath, "Assembly.Load", path => Assembly.Load(File.ReadAllBytes(path)));
        }

        private static Assembly Load(string dllPath, string description, Func<string, Assembly> load)
        {
            try
            {
                return load(dllPath);
            }
            catch (ReflectionTypeLoadException ex)
            {
                var errMessage = new StringBuilder();
                errMessage.AppendLine(string.Format("Error in {0}({1}) at", description, dllPath));
                errMessage.AppendLine(ex.StackTrace);
                errMessage.AppendLine();
                errMessage.AppendLine(string.Format(ex.Message));
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    errMessage.AppendLine();
                    errMessage.AppendLine(loaderException.Message);
                }
                throw new Exception(errMessage.ToString(), ex);
            }
        }
    }

    public class InvokeSkyline
    {
        private readonly Type _skylineProgram;
        // The process globals that do not need the UI (test flags, Name) are on ProgramContext in
        // SkylineCore, the rest on Program; a member is looked for on Program first
        private readonly Type[] _globalsTypes;

        public InvokeSkyline()
        {
            // On net8 the managed entry point is Skyline-daily.dll; the .exe is a native
            // apphost that Assembly.LoadFrom can't load ("Bad IL format"). net472 loads the .exe.
            var skylinePath = GetAssemblyPath("Skyline-daily.dll"); // Keep -daily
            if (!File.Exists(skylinePath))
                skylinePath = GetAssemblyPath("Skyline.dll");
            var skylineAssembly = LoadFromAssembly.Try(skylinePath);
            _skylineProgram = skylineAssembly.GetType("pwiz.Skyline.Program");
            // Loaded through Skyline's own reference, so this is the instance Skyline uses
            var coreAssembly = Assembly.Load(skylineAssembly.GetReferencedAssemblies()
                .First(name => name.Name == "SkylineCore"));
            _globalsTypes = new[] { _skylineProgram, coreAssembly.GetType("pwiz.Skyline.ProgramContext") };
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
            GetGlobalsMethod("set_" + field).Invoke(null, new[] {value});
        }

        public T Get<T>(string field)
        {
            return (T) GetGlobalsMethod("get_" + field).Invoke(null, null);
        }

        private MethodInfo GetGlobalsMethod(string name)
        {
            return _globalsTypes.Select(type => type.GetMethod(name)).FirstOrDefault(method => method != null)
                   ?? throw new MissingMethodException(string.Join(" or ", _globalsTypes.Select(type => type.FullName)), name);
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
