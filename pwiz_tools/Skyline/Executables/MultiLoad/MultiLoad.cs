using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MultiLoad
{
    public partial class MultiLoad : Form
    {
        private const string RootDir = @"C:\Users\donmarsh\Documents\Data"; //@"D:\Data\20150331_Pedro_TTOF5600_64w";
        private const string SkylineFile = @"TTOF_64w.sky";
        private const string SkydFile = RootDir + @"\" + SkylineFile + "d";
        private const string SkylineExe = @"D:\pwiz\pwiz_tools\Skyline\bin\x64\Release\Skyline-daily.exe";
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

            _stopwatch = new Stopwatch();
            _stopwatch.Start();
            _timer = new Timer {Interval = 500};
            _timer.Tick += (s, e1) => lblTime.Text = _stopwatch.Elapsed.ToString(@"mm\:ss"); // Not L10N
            _timer.Start();
            
            if (File.Exists(SkydFile))
                File.Delete(SkydFile);

            _log = Log.Replace("#", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            _dataDir = Path.Combine(RootDir, comboModel.Text);

            _uiIndex = comboUI.SelectedIndex;
            _queue = new QueueWorker<string>(Run) {CompleteAction = RunFinished};
            _queue.RunAsync((int) numericMaxProcesses.Value, "Start Skyline");
            _queue.Add(Directory.EnumerateFiles(_dataDir, comboModel.Text == "wiff" ? "*.wiff" : "*.mz5"));
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
                File.AppendAllText(_log, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
            }
        }
    }
}
