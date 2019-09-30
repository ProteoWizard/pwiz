using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MultiLoad
{
    public partial class MultiLoad : Form
    {
        private const string RootDir = @"C:\Users\donmm\Documents\Data"; //@"D:\Data\20150331_Pedro_TTOF5600_64w";
        private const string SkylineFile = @"TTOF_64w.sky";
        private const string SkylineExe = @"D:\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline-daily.exe";  // Keep -daily
        private const string Log = RootDir + @"\MultiLoad_#.log";

        private string _log;
        private QueueWorker<string> _queue;
        private Stopwatch _stopwatch;
        private Timer _timer;
        private Color _timeColor;
        private string _dataDir;
        private int _uiIndex;

        public MultiLoad()
        {
            InitializeComponent();
            comboUI.SelectedIndex = 0;
            comboModel.SelectedIndex = 0;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_queue != null)
                _queue.Abort();
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            base.OnClosing(e);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            _timeColor = lblTime.ForeColor;
            lblTime.ForeColor = Color.Red;

            DeleteFiles(RootDir, "*.skyd");
            DeleteFiles(RootDir, "*.tmp");

            _log = Log.Replace("#", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            _timer = new Timer {Interval = 500};
            _timer.Tick += (s, e1) => lblTime.Text = _stopwatch.Elapsed.ToString(@"mm\:ss"); // Not L10N
            _timer.Start();

            _dataDir = Path.Combine(RootDir, comboModel.Text);
            var extension = 
                comboModel.Text.StartsWith("mz5") ? ".mz5" :
                comboModel.Text.StartsWith("mzml") ? ".mzML" :
                ".wiff";
            var dataFiles = Directory.EnumerateFiles(_dataDir).Where(s => Path.GetExtension(s) == extension).ToList();

            _uiIndex = comboUI.SelectedIndex;
            if (_uiIndex == 4)
            {
                var args = string.Format(
                    @"--timestamp --dir=""{0}"" --in=""{1}"" --import-no-join --importthreads={2}",
                    RootDir,
                    SkylineFile,
                    (int)numericMaxProcesses.Value);
                args = dataFiles.Aggregate(args, (current, dataFile) => current + string.Format(@" --import-file=""{0}""", dataFile));
                //args += string.Format(@" --import-file=""{0}""", dataFiles[0]);

                _queue = new QueueWorker<string>(RunCommandLine) { CompleteAction = RunFinished };
                _queue.RunAsync(1, "Run command line");
                _queue.Add(args);
                _queue.DoneAdding();
            }
            else
            {
                _queue = new QueueWorker<string>(Run) {CompleteAction = RunFinished};
                _queue.RunAsync((int) numericMaxProcesses.Value, "Start Skyline");
                _queue.Add(dataFiles);
            }
        }

        private static void DeleteFiles(string directory, string pattern)
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private void RunCommandLine(string args, int threadIndex)
        {
            AddToLog("Start command line");
            AddToLog(args);
            var runner = new Process
            {
                StartInfo =
                {
                    FileName = SkylineExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            runner.OutputDataReceived += HandleOutput;
            runner.ErrorDataReceived += HandleOutput;
            runner.Start();
            runner.BeginOutputReadLine();
            runner.BeginErrorReadLine();
            runner.WaitForExit();
            AddToLog("End command line");
        }

        private void HandleOutput(object sender, DataReceivedEventArgs e)
        {
            AddToLog(e.Data);
        }

        private void RunFinished()
        {
            _queue = null;
            _stopwatch = null;
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
                Invoke(new Action(() => btnStart.Enabled = true));
            }
            lblTime.ForeColor = _timeColor;
        }

        private void Run(string dataFile, int threadIndex)
        {
            AddToLog("Start " + Path.GetFileName(dataFile));
            var uiOptions = new[] {"", "--ui ", "--ui --hideacg ", "--ui --noacg "};
            var args = string.Format(
                @"{0}--timestamp --dir=""{1}"" --in=""{2}"" --import-file=""{3}"" --import-no-join",
                uiOptions[_uiIndex],
                RootDir,
                SkylineFile,
                dataFile);
            AddToLog(args);
            var runner = new Process
            {
                StartInfo =
                {
                    FileName = SkylineExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            runner.Start();
            var output = runner.StandardOutput.ReadToEnd();
            lock (_log)
            {
                File.AppendAllText(_log, output);
            }
            AddToLog("End   " + Path.GetFileName(dataFile));
        }

        private void AddToLog(string text)
        {
            lock (_log)
            {
                File.AppendAllText(_log, text + Environment.NewLine);
            }
        }
    }
}
