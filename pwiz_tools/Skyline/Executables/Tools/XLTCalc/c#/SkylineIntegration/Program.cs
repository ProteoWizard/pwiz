using System;
using System.Diagnostics;
using System.Windows.Forms;
using SkylineTool;

namespace SkylineIntegration
{
    static class Program
    {
        private static SkylineToolClient _toolClient;
        private static string _inputFile;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _toolClient = new SkylineToolClient(args[0], "Example Interactive Tool"); // Not L10N

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "All Files (*.*)|*.*";
            ofd.FilterIndex = 1;
            ofd.Multiselect = false;

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _inputFile = ofd.FileName;
            }
            else
            {
                Console.WriteLine("Cancelled");
            }

            if (_inputFile != null)
            {
                var res = runCommand();
//                res = "PrecursorName\tPrecursorMz\tProductMz\tPrecursorCharge\tProductCharge\tMoleculeGroup\tProductName\r\n" + res;
                if (res.Item1 != 0)
                {
                    return;
                }

                var input = res.Item2.Replace("\t", ",");

                try
                {
                    _toolClient.InsertSmallMoleculeTransitionList(input);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
            _toolClient.Dispose();
            Application.Exit();
        }

        private static Tuple<int,string> runCommand()
        {
            Process process = new Process();
            process.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "react2prm_web.exe";
            process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            process.StartInfo.Arguments = "-f " + _inputFile;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();
            //* Read the output (or the error)
            string output = process.StandardOutput.ReadToEnd();

            string err = process.StandardError.ReadToEnd();
            Console.WriteLine(err);
            process.WaitForExit();
            if(process.ExitCode != 0)
                return new Tuple<int, string>(process.ExitCode, err);
            return new Tuple<int, string>(process.ExitCode, output);
        }
    }
}
