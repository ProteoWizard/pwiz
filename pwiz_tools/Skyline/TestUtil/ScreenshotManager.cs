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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Util;
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

        public static Rectangle GetWindowRectangle(Control ctrl, bool fullScreen = false)
        {
            var snapshotBounds = Rectangle.Empty;

            var dockedStates = new[] { DockState.DockBottom, DockState.DockLeft, DockState.DockRight, DockState.DockTop, DockState.Document };
            var dockableForm = ctrl as DockableForm;
            if (dockableForm != null && dockedStates.Any((state) => dockableForm.DockState == state))
            {
                Point origin = Point.Empty;
                dockableForm.Invoke(new Action(() => { origin = dockableForm.Pane.PointToScreen(new Point(0, 0)); }));
                snapshotBounds = new Rectangle(origin, dockableForm.Pane.Size);
            }
            else if (fullScreen)
            {
                snapshotBounds = Screen.FromControl(ctrl).Bounds;
            }
            else
            {
                ctrl = FindParent<FloatingWindow>(ctrl) ?? ctrl;
                int width = (ctrl as Form)?.DesktopBounds.Width ?? ctrl.Width;
                int frameWidth = (width - ctrl.ClientRectangle.Width) / 2 - SystemInformation.Border3DSize.Width + SystemInformation.BorderSize.Width;
                Size imageSize = ctrl.Size + new PointAdditive(-2 * frameWidth, -frameWidth);
                Point sourcePoint = ctrl.Location + new PointAdditive(frameWidth, 0);
                snapshotBounds = new Rectangle(sourcePoint, imageSize);
            }
            return snapshotBounds * GetScalingFactor();
        }

        public static TParent FindParent<TParent>(Control ctrl) where TParent : Control
        {
            while (ctrl != null)
            {
                if (ctrl is TParent parent)
                    return parent;
                ctrl = ctrl.Parent;
            }

            return null;
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
            public static SkylineScreenshot CreateScreenshot(Control control, bool fullScreen = false)
            {
                if (control is ZedGraphControl zedGraphControl)
                {
                    return new ZedGraphShot(zedGraphControl);
                }
                else
                {
                    return new ActiveWindowShot(control, fullScreen);
                }
            }
            public abstract Bitmap Take();
        }

        private class ActiveWindowShot : SkylineScreenshot
        {
            private readonly Control _activeWindow;
            private readonly bool _fullscreen;
            
            public ActiveWindowShot(Control activeWindow, bool fullscreen)
            {
                _activeWindow = activeWindow;
                _fullscreen = fullscreen;
            }

            [NotNull]
            public override Bitmap Take()
            {
                Rectangle shotFrame = GetWindowRectangle(_activeWindow, _fullscreen);
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

        public static void ActivateScreenshotForm(Control screenshotControl)
        {
            Assume.IsTrue(screenshotControl.InvokeRequired);    // Use ActionUtil.RunAsync() to call this method from the UI thread

            // Bring to the front
            RunUI(screenshotControl, screenshotControl.SetForegroundWindow);

            // If it is a form, try not to change the focus within the form.
            var form = FormUtil.FindParentOfType<Form>(screenshotControl)?.ParentForm;
            if (form != null)
            {
                RunUI(form, () => form.Activate());
            }
            else
            {
                RunUI(screenshotControl,() => screenshotControl.Focus());
            }

            Thread.Sleep(200);  // Allow activation message processing on the UI thread

            RunUI(screenshotControl, () =>
            {
                var focusText = screenshotControl.GetFocus() as TextBox;
                if (focusText != null)
                {
                    focusText.Select(focusText.Text.Length, 0);
                    focusText.HideCaret();
                }
            });

            Thread.Sleep(10);   // Allow selection to repaint on the UI thread
        }

        private static void RunUI(Control control, Action action)
        {
            control.Invoke(action);
        }

        public Bitmap TakeShot(Control activeWindow, bool fullScreen = false, string pathToSave = null, Func<Bitmap, Bitmap> processShot = null, double? scale = null)
        {
            activeWindow ??= _skylineWindow;

            //check UI and create a blank shot according to the user selection
            SkylineScreenshot newShot = SkylineScreenshot.CreateScreenshot(activeWindow, fullScreen);

            Bitmap shotPic = newShot.Take();
            if (processShot != null)
            {
                // execute on window's thread in case delegate accesses UI controls
                shotPic = activeWindow.Invoke(processShot, shotPic) as Bitmap;
                Assert.IsNotNull(shotPic);
            }
            else
            {
                // Tidy up annoying variations in screenshot border due to underlying windows
                // Only for unprocessed window screenshots
                CleanupBorder(shotPic); 
            }

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
            var clipThread = new Thread(() => Clipboard.SetImage(shotPic));
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

