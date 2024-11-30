using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
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
        private readonly SkylineWindow _skylineWindow;
        private readonly string _tutorialSourcePath;
        private readonly string _tutorialDestPath;

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

        public static Rectangle GetWindowRectangle(Control ctrl, bool fullScreen = false, bool scale = true)
        {
            var snapshotBounds = Rectangle.Empty;

            var dockedStates = new[] { DockState.DockBottom, DockState.DockLeft, DockState.DockRight, DockState.DockTop, DockState.Document };
            var dockableForm = ctrl as DockableForm;
            if (dockableForm != null && dockedStates.Any(state => dockableForm.DockState == state))
            {
                snapshotBounds = GetDockedFormBounds(dockableForm);
            }
            else if (fullScreen)
            {
                snapshotBounds = (Rectangle)ctrl.Invoke((Func<Rectangle>) (() => Screen.FromControl(ctrl).Bounds));
            }
            else
            {
                snapshotBounds = GetFramedWindowBounds(ctrl);
            }
            return scale ? snapshotBounds * GetScalingFactor() : snapshotBounds;
        }

        public static Rectangle GetDockedFormBounds(DockableForm ctrl)
        {
            return ctrl.InvokeRequired
                ? (Rectangle)ctrl.Invoke((Func<Rectangle>)(() => GetDockedFormBoundsInternal(ctrl)))
                : GetDockedFormBoundsInternal(ctrl);
        }

        public static Rectangle GetDockedFormBoundsInternal(DockableForm dockedForm)
        {
            var parentRelativeVBounds = dockedForm.Pane.Bounds;
            // The pane bounds do not include the border
            parentRelativeVBounds.Inflate(SystemInformation.BorderSize.Width, SystemInformation.BorderSize.Width);
            return dockedForm.Pane.Parent.RectangleToScreen(parentRelativeVBounds);
        }
        
        public static Rectangle GetFramedWindowBounds(Control ctrl)
        {
            ctrl = FindParent<FloatingWindow>(ctrl) ?? ctrl;
            return ctrl.InvokeRequired 
                ? (Rectangle) ctrl.Invoke((Func<Rectangle>)(() => GetFramedWindowBoundsInternal(ctrl)))
                : GetFramedWindowBoundsInternal(ctrl);
        }

        private static Rectangle GetFramedWindowBoundsInternal(Control ctrl)
        {
            int width = (ctrl as Form)?.DesktopBounds.Width ?? ctrl.Width;
            // The drop shadow is 1/2 the difference between the window width and the client rect width
            // A 3D border width is removed from this and then a standard border width (usually 1) removed
            int dropShadowWidth = (width - ctrl.ClientRectangle.Width) / 2 - SystemInformation.Border3DSize.Width + SystemInformation.BorderSize.Width;
            Size imageSize;
            Point sourcePoint;
            if (ctrl is Form)
            {
                // The snapshot size then removes the shadow width on both sides and from only the bottom
                imageSize = ctrl.Size + new PointAdditive(-2 * dropShadowWidth, -dropShadowWidth);
                // And the origin is shifted one shadow width to the right
                sourcePoint = ctrl.Location + new PointAdditive(dropShadowWidth, 0);
            }
            else
            {
                // Otherwise, it is just a control on a form without a drop shadow
                imageSize = ctrl.Size;
                sourcePoint = ctrl.Parent.PointToScreen(ctrl.Location);
            }
            return new Rectangle(sourcePoint, imageSize);
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
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = g.GetHdc();
            int LogicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
            int PhysicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);
            float ScreenScalingFactor = PhysicalScreenHeight / (float)LogicalScreenHeight;

            return new PointFactor(ScreenScalingFactor); // 1.25 = 125%
        }

        private abstract class SkylineScreenshot
        {
            /// <summary>
            /// Returns a new instance of the right type of screenshot
            /// </summary>
            /// <param name="control">A control to create a screenshot for</param>
            /// <param name="fullScreen">True if it should be fullscreen</param>
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
                var shotRect = GetWindowRectangle(_activeWindow, _fullscreen);
                var bmpCapture = new Bitmap(shotRect.Width, shotRect.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmpCapture);
                while (!CaptureFromScreen(g, shotRect))
                {
                    Thread.Sleep(1000); // Try again in one second - remote desktop may be minimized
                }
                return bmpCapture;
            }

            private static bool CaptureFromScreen(Graphics g, Rectangle shotRect)
            {
                try
                {
                    g.CopyFromScreen(shotRect.Location,
                        Point.Empty, shotRect.Size);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
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
                var emf = _zedGraphControl.MasterPane.GetMetafile();
                var bmp = new Bitmap(emf.Width, emf.Height);
                bmp.SetResolution(emf.HorizontalResolution, emf.VerticalResolution);
                using var g = Graphics.FromImage(bmp);
                g.DrawImage(emf, 0, 0);
                return bmp;
            }

        }

        public ScreenshotManager([NotNull] SkylineWindow pSkylineWindow, string tutorialPath)
        {
            _skylineWindow = pSkylineWindow;
            _tutorialDestPath = tutorialPath;
            _tutorialSourcePath = GetAvailableTutorialPath(tutorialPath);
        }

        private string GetAvailableTutorialPath(string tutorialPath)
        {
            if (!string.IsNullOrEmpty(tutorialPath) && !Directory.Exists(tutorialPath))
            {
                var langLetters = AbstractFunctionalTest.GetFolderNameForLanguage(CultureInfo.CurrentCulture);
                var langLettersInvariant = AbstractFunctionalTest.GetFolderNameForLanguage(CultureInfo.InvariantCulture);
                if (!Equals(langLettersInvariant, langLetters) && tutorialPath.EndsWith(langLetters))
                {
                    tutorialPath = tutorialPath.Substring(0, tutorialPath.Length - langLetters.Length) + langLettersInvariant;
                }
            }
            return tutorialPath;
        }

        private SkylineWindow SkylineWindow => Program.MainWindow;

        public string ScreenshotUrl(int screenshotNum)
        {
            if (string.IsNullOrEmpty(_tutorialSourcePath))
                return null;
            return GetTutorialUrl("index.html") + "#s-" + PadScreenshotNum(screenshotNum);
        }

        public string ScreenshotImgUrl(int screenshotNum)
        {
            return GetTutorialUrl("s-" + PadScreenshotNum(screenshotNum) + ".png");
        }

        private const string SCREENSHOT_URL_FOLDER = "24-1";

        private string GetTutorialUrl(string filePart)
        {
            if (string.IsNullOrEmpty(_tutorialSourcePath))
                return null;
            var fileUri = new Uri(Path.Combine(_tutorialSourcePath, filePart)).AbsoluteUri;
            const string tutorialSearch = "/Tutorials/";
            int tutorialIndex = fileUri.IndexOf(tutorialSearch, StringComparison.Ordinal);
            return "https://skyline.ms/tutorials/" + SCREENSHOT_URL_FOLDER + "/" + fileUri.Substring(tutorialIndex + tutorialSearch.Length);
        }

        public string ScreenshotSourceFile(int screenshotNum)
        {
            return !string.IsNullOrEmpty(_tutorialSourcePath) ? $"{Path.Combine(_tutorialSourcePath, "s-" + PadScreenshotNum(screenshotNum))}.png" : null;
        }

        public string ScreenshotDestFile(int screenshotNum)
        {
            return !string.IsNullOrEmpty(_tutorialDestPath) ? $"{Path.Combine(_tutorialDestPath, "s-" + PadScreenshotNum(screenshotNum))}.png" : null;
        }

        private static string PadScreenshotNum(int screenshotNum)
        {
            return screenshotNum.ToString("D2");
        }

        public string ScreenshotDescription(int i, string description)
        {
            return string.Format("s-{0}: {1}", i, description);
        }

        public bool IsOverlappingScreenshot(Rectangle bounds)
        {
            var skylineRect = GetScreenshotBounds();
            return !Rectangle.Intersect(skylineRect, bounds).IsEmpty;
        }

        /// <summary>
        /// Returns the bounds of the area reserved for the screenshot.
        /// Currently, the bounds of the SkylineWindow is returned, which
        /// is imperfect because there is no guarantee that the screenshot
        /// will not be taken outside those bounds. If we knew the true
        /// bounds of the screenshot at this point, we would probably want
        /// the union of them and the Skyline bounds, since avoiding covering
        /// the SkylineWindow is still useful.
        /// </summary>
        public Rectangle GetScreenshotBounds()
        {
            return (Rectangle)SkylineWindow.Invoke((Func<Rectangle>)(() => SkylineWindow.Bounds));
        }

        public Rectangle GetScreenshotScreenBounds()
        {
            return GetScreenshotScreen().Bounds;
        }

        public Screen GetScreenshotScreen()
        {
            return (Screen)SkylineWindow.Invoke((Func<Screen>)(() => Screen.FromControl(SkylineWindow)));
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
                RunUI(form, () =>
                {
                    FormEx.ForceOnScreen(form); // Make sure the owning form is fully on screen
                    form.Activate();
                });
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

            // Check UI and create a blank shot according to the user selection
            var newShot = SkylineScreenshot.CreateScreenshot(activeWindow, fullScreen);

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

            // Have to do it this way because of the limitation on OLE access from background threads.
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

