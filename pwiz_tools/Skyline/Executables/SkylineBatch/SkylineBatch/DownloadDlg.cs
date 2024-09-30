using SharedBatch;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using pwiz.PanoramaClient;
using Timer = System.Windows.Forms.Timer;

namespace SkylineBatch
{
    public partial class DownloadDlg : Form
    {
        private PanoramaFile _panoramaFile;
        private DataServerInfo _server;
        private CancellationTokenSource _source;
        private int _percent;
        private Timer _timer;
        private bool _loaded;

        public DownloadDlg(PanoramaFile panoramaFile, DataServerInfo server)
        {
            InitializeComponent();
            Icon = Program.Icon();

            _panoramaFile = panoramaFile;
            _server = server;
            _source = new CancellationTokenSource();

            _timer = new Timer { Interval = 10 };
            _timer.Tick += ((sender, e) =>
            {
                if (!_loaded) return;
                BeginInvoke((MethodInvoker)delegate { progressBar.Value = _percent; });
            });
            _timer.Start();

            Shown += (sender, e) =>
            {
                Download();
                _loaded = true;
            };
        }

        private void DownloadDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            _source.Cancel();
            _timer.Stop();
        }

        private void Download()
        {
            new Thread( async () =>
            {
                using (var wc = new WebClient())
                {
                    var realName = Path.Combine(_panoramaFile.DownloadFolder, _panoramaFile.FileName);
                    var panoramaServerUri = new Uri(Uri.UnescapeDataString(_server.URI.GetLeftPart(UriPartial.Authority)));
                    var downloadUri = new Uri(_panoramaFile.DownloadUrl);
                    var size = PanoramaServerConnector.GetSize(downloadUri, panoramaServerUri,
                        new WebPanoramaClient(panoramaServerUri, _server.FileSource.Username,
                        _server.FileSource.Password),
                        new CancellationToken());

                    _source.Token.Register(wc.CancelAsync);

                    using (var fs = new FileSaver(realName))
                    {
                        try
                        {
                            wc.DownloadProgressChanged += (sender, e) =>
                            {
                                _percent = Math.Min((int)((double)e.BytesReceived / size * 100), 99);
                            };

                            await wc.DownloadFileTaskAsync(downloadUri, fs.SafeName);
                            fs.Commit();
                            if (Visible) BeginInvoke((MethodInvoker)delegate { Close(); });
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }).Start();
        }
    }
}