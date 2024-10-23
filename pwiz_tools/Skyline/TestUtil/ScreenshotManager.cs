using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using JetBrains.Annotations;
using pwiz.Skyline;
using ZedGraph;

namespace pwiz.SkylineTestUtil
{
    public class ScreenshotManager
    {
        private SkylineWindow _skylineWindow;

        public class PointFactor
        {
            private float _factor;

            public PointFactor(float pFactor) { _factor = pFactor; }
            
            public static Point operator *(Point pt, PointFactor pFactor) => new Point((int)Math.Round(pt.X * pFactor._factor), (int)Math.Round(pt.Y * pFactor._factor));
            public static Size operator *(Size sz, PointFactor pFactor) => new Size((int)Math.Round(sz.Width * pFactor._factor), (int)Math.Round(sz.Height * pFactor._factor));
            public static Rectangle operator *(Rectangle rect, PointFactor pFactor) => new Rectangle(rect.Location * pFactor, rect.Size * pFactor);
        }

        public class PointAdditive
        {
            private Point _add;
            public PointAdditive(Point pAdd) { _add = pAdd; }
            public PointAdditive(int pX, int pY) { _add = new Point(pX, pY); }
            public static PointAdditive operator +(Point pt, PointAdditive pAdd) => new PointAdditive(new Point(pt.X + pAdd._add.X, pt.Y + pAdd._add.Y));
            public static Size operator +(Size sz, PointAdditive pAdd) => new Size(sz.Width + pAdd._add.X, sz.Height + pAdd._add.Y);

            public static implicit operator Point(PointAdditive add) => add._add;
        }

        public static Rectangle GetWindowRectangle(Control ctrl)
        {
            Rectangle snapshotBounds = Rectangle.Empty;

            DockState[] dockedStates = new DockState[] { DockState.DockBottom, DockState.DockLeft, DockState.DockRight, DockState.DockTop, DockState.Document };
            var form = ctrl as DockableForm;
            if (form != null && dockedStates.Any((state) => form.DockState == state))
            {
                Point origin = Point.Empty;
                ctrl.Invoke(new Action(() => { origin = form.Pane.PointToScreen(new Point(0, 0)); }));
                snapshotBounds = new Rectangle(origin, form.Pane.Size);
            }
            else
            {
                //TODO BEFORE MERGE: figure out what to do when it is not a form
                if (ctrl is Form && (ctrl as Form).ParentForm is FloatingWindow)
                    ctrl = (ctrl as Form).ParentForm;
                int frameWidth = ((ctrl as Form).DesktopBounds.Width - ctrl.ClientRectangle.Width) / 2 - SystemInformation.Border3DSize.Width + SystemInformation.BorderSize.Width;
                Size imageSize = ctrl.Size + new PointAdditive(-2 * frameWidth, -frameWidth);
                Point sourcePoint = ctrl.Location + new PointAdditive(frameWidth, 0);
                snapshotBounds = new Rectangle(sourcePoint, imageSize);

            }
            return snapshotBounds * GetScalingFactor();
        }

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private enum DeviceCap
        {
            VERTRES = 10,
            DESKTOPVERTRES = 117,
        }

        public static PointFactor GetScalingFactor()
        {
            Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = g.GetHdc();
            int LogicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
            int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);

            float ScreenScalingFactor = PhysicalScreenHeight / (float)LogicalScreenHeight;

            return new PointFactor(ScreenScalingFactor); // 1.25 = 125%
        }
        private abstract class SkylineScreenshot
        {
            /**
             * Factory method
             */
            public static SkylineScreenshot CreateScreenshot(Control control)
            {
                SkylineScreenshot newShot;
                if (control is ZedGraphControl zedGraphControl)
                {
                    newShot = new ZedGraphShot(zedGraphControl);
                }
                else
                {
                    newShot = new ActiveWindowShot(control);
                }

                return newShot;
            }
            public abstract Bitmap Take();
        }

        private class ActiveWindowShot : SkylineScreenshot
        {
            private readonly Control _activeWindow;
            public ActiveWindowShot(Control activeWindow)
            {
                _activeWindow = activeWindow;
            }

            [NotNull]
            public override Bitmap Take()
            {
                Rectangle shotFrame = GetWindowRectangle(_activeWindow);
                Bitmap bmCapture = new Bitmap(shotFrame.Width, shotFrame.Height, PixelFormat.Format32bppArgb);
                Graphics graphCapture = Graphics.FromImage(bmCapture);
                bool captured = false;
                while (!captured)
                {
                    try
                    {
                        graphCapture.CopyFromScreen(shotFrame.Location,
                            new Point(0, 0), shotFrame.Size);
                        captured = true;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000); // Try again in one second - remote desktop may be minimized
                    }
                }
                graphCapture.Dispose();
                return bmCapture;
            }
        }

        private class ZedGraphShot : SkylineScreenshot
        {
            private readonly ZedGraphControl _zedGraphControl;
            public ZedGraphShot(ZedGraphControl zedGraphControl)
            {
                _zedGraphControl = zedGraphControl;
            }
            public override Bitmap Take()
            {
                Metafile emf = (_zedGraphControl.MasterPane.GetMetafile());
                Bitmap bmp = new Bitmap(emf.Width, emf.Height);
                bmp.SetResolution(emf.HorizontalResolution, emf.VerticalResolution);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(emf, 0, 0);
                }
                return bmp;
            }

        }
        public ScreenshotManager([NotNull] SkylineWindow pSkylineWindow)
        {
            _skylineWindow = pSkylineWindow;
        }


        public Bitmap TakeShot(Control activeWindow, string pathToSave = null, Func<Bitmap, Bitmap> processShot = null, double? scale = null)
        {
            if (activeWindow == null)
                activeWindow = _skylineWindow;

            //check UI and create a blank shot according to the user selection
            SkylineScreenshot newShot = SkylineScreenshot.CreateScreenshot(activeWindow);

            Bitmap shotPic = newShot.Take();
            shotPic = processShot != null ? processShot.Invoke(shotPic) : shotPic;
            CleanupBorder(shotPic); // Tidy up annoying variations in screenshot border due to underlying windows

            if (scale.HasValue)
            {
                shotPic = new Bitmap(shotPic,
                    (int) Math.Round(shotPic.Width * scale.Value),
                    (int) Math.Round(shotPic.Height * scale.Value));
            }

            if (pathToSave != null)
            {
                SaveToFile(pathToSave, shotPic);
            }

            //Have to do it this way because of the limitation on OLE access from background threads.
            Thread clipThread = new Thread(() => Clipboard.SetImage(shotPic));
            clipThread.SetApartmentState(ApartmentState.STA);
            clipThread.Start();
            clipThread.Join();

            return shotPic;
        }

        private static void CleanupBorder(Bitmap shotPic)
        {
            // Determine border color, then make it consistently that color
            var stats = new Dictionary<Color, int>();

            void UpdateStats(int x, int y)
            {
                var c = shotPic.GetPixel(x, y);
                if (stats.ContainsKey(c))
                {
                    stats[c]++;
                }
                else
                {
                    stats[c] = 1;
                }
            }

            for (var x = 0; x < shotPic.Width; x++)
            {
                UpdateStats(x, 0);
                UpdateStats(x, shotPic.Height - 1);
            }

            for (var y = 0; y < shotPic.Height; y++)
            {
                UpdateStats(0, y);
                UpdateStats(shotPic.Width - 1, y);
            }

            var color = stats.FirstOrDefault(kvp => kvp.Value == stats.Values.Max()).Key;

            // Enforce a clean border
            for (var x = 0; x < shotPic.Width; x++)
            {
                shotPic.SetPixel(x, 0, color);
                shotPic.SetPixel(x, shotPic.Height - 1, color);
            }

            for (var y = 0; y < shotPic.Height; y++)
            {
                shotPic.SetPixel(0, y, color);
                shotPic.SetPixel(shotPic.Width - 1, y, color);
            }
        }

        private void SaveToFile(string filePath, Bitmap bmp)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            bmp.Save(filePath);
        }
    }

}

