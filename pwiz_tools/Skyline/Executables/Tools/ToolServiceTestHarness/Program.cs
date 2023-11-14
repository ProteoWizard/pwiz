using System.Diagnostics;

namespace ToolServiceTestHarness
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arguments)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            var form = new ToolServiceTestHarnessForm();
            if (arguments.Length > 0)
            {
                form.ConnectionName = arguments[0];
            }
            Application.Run(form);
        }
    }
}