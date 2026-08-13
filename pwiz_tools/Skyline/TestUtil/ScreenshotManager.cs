/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util;
using TestRunnerLib.PInvoke;
using ZedGraph;

namespace pwiz.SkylineTestUtil
{
    public class ScreenshotManager
    {
        private readonly SkylineWindow _skylineWindow;
        private readonly string _tutorialSourcePath;
        private readonly string _tutorialDestPath;

        // Delegate to ScreenCapture types for backward compatibility
        public class PointFactor : ScreenCapture.PointFactor
        {
            public PointFactor(float pFactor) : base(pFactor) { }
        }

        // Delegate to ScreenCapture types for backward compatibility
        public class PointAdditive : ScreenCapture.PointAdditive
        {
            public PointAdditive(Point pAdd) : base(pAdd) { }
            public PointAdditive(int pX, int pY) : base(pX, pY) { }
        }

        public static Rectangle GetWindowRectangle(Control ctrl, bool fullScreen = false, bool scale = true)
        {
            return ScreenCapture.GetWindowRectangle(ctrl, fullScreen, scale);
        }

        public static Rectangle GetDockedFormBounds(DockableForm ctrl)
        {
            return ScreenCapture.GetDockedFormBounds(ctrl);
        }

        public static Rectangle GetFramedWindowBounds(Control ctrl)
        {
            return ScreenCapture.GetFramedWindowBounds(ctrl);
        }

        public static TParent FindParent<TParent>(Control ctrl) where TParent : Control
        {
            return ScreenCapture.FindParent<TParent>(ctrl);
        }

        public static ScreenCapture.PointFactor GetScalingFactor()
        {
            return ScreenCapture.GetScalingFactor();
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
                var shotRect = ScreenCapture.GetWindowRectangle(_activeWindow, _fullscreen);
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

        private class RectangleShot : SkylineScreenshot
        {
            private readonly Rectangle _screenRect;

            public RectangleShot(Rectangle screenRect)
            {
                _screenRect = screenRect;
            }

            [NotNull]
            public override Bitmap Take()
            {
                var scaledRect = _screenRect * ScreenCapture.GetScalingFactor();
                var bmpCapture = new Bitmap(scaledRect.Width, scaledRect.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmpCapture);
                g.CopyFromScreen(scaledRect.Location, Point.Empty, scaledRect.Size);
                return bmpCapture;
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
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(emf, 0, 0);
                }
                // Need to use a consistent resolution or PNGs will differ depending on the monitor they were rendered on
                // You can get PNGs that are pixel for pixel identical but the PNGs will differ in the pHYs blocks
                // Original resolution on ASUS 28" monitors - a modern laptop had much higher DPI
                // Hex values taken from binary editor for original files
                bmp.SetResolution(DpmToDpi(0x0C13), DpmToDpi(0x0C5F));
                return bmp;
            }

            /// <summary>
            /// Converts dots per meter (DPM) found in pHYs blocks in PNG files to DPI used in Bitmap resolution.
            /// </summary>
            /// <param name="dpm">A dots per meter integer</param>
            /// <returns>A dots per inch (DPM/39.37) value</returns>
            private float DpmToDpi(int dpm)
            {
                return (float)(dpm / 39.37);
            }
        }

        public ScreenshotManager([NotNull] SkylineWindow skylineWindow, string tutorialPath)
        {
            Assume.IsNotNull(skylineWindow);

            _skylineWindow = skylineWindow;
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

        public static string PadScreenshotNum(int screenshotNum)
        {
            return screenshotNum.ToString("D2");
        }

        public string ScreenshotDescription(int i, string description)
        {
            return string.Format("s-{0}: {1}", PadScreenshotNum(i), description);
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
            return (Rectangle)_skylineWindow.Invoke((Func<Rectangle>)(() => _skylineWindow.Bounds));
        }

        public Rectangle GetScreenshotScreenBounds()
        {
            return GetScreenshotScreen().Bounds;
        }

        public Screen GetScreenshotScreen()
        {
            return Screen.FromRectangle(GetScreenshotBounds());
        }

        private void ForceOnScreenshotScreen(Form form)
        {
            var location = form.Location;
            var screen = GetScreenshotScreen();
            location.X = Math.Max(screen.WorkingArea.Left,
                Math.Min(location.X, screen.WorkingArea.Right - form.Size.Width));
            location.Y = Math.Max(screen.WorkingArea.Top,
                Math.Min(location.Y, screen.WorkingArea.Bottom - form.Size.Height));
            form.Location = location;
        }

        public void ActivateScreenshotForm(Control screenshotControl)
        {
            Assume.IsTrue(screenshotControl.InvokeRequired);    // Use ActionUtil.RunAsync() to call this method from the UI thread

            // Bring to the front
            RunUI(screenshotControl, screenshotControl.SetForegroundWindow);

            // If there is a form, try not to change the focus within the form.
            var form = FormUtil.FindParentOfType<Form>(screenshotControl)?.ParentForm ?? screenshotControl as Form;
            if (form != null)
            {
                RunUI(form, () =>
                {
                    ForceOnScreenshotScreen(form); // Make sure the owning form is fully on screen
                    form.Activate();
                });
            }
            else
            {
                RunUI(screenshotControl,() => screenshotControl.Focus());
            }

            Thread.Sleep(500);  // Allow activation message processing on the UI thread

            RunUI(screenshotControl, () => HideSensitiveFocusDisplay(screenshotControl));

            Thread.Sleep(10);   // Allow selection to repaint on the UI thread
        }

        /// <summary>
        /// Attempt to get more consistent screenshots by hiding sensitive
        /// display elements indicating a control has the focus, like blinking
        /// cursors and dotted rectangles on tab controls.
        /// </summary>
        private void HideSensitiveFocusDisplay(Control screenshotControl)
        {
            // Hide focus rectangles and mnemonic underscores by sending WM_CHANGEUISTATE
            // to the top-level form. Windows tracks keyboard vs mouse mode and shows these
            // UI cues when in keyboard mode. This can happen even when tests are started
            // with a mouse click if any key is pressed on the machine.
            HideKeyboardCues(screenshotControl);

            var focusControl = screenshotControl.GetFocus();
            if (focusControl is TextBox focusText)
            {
                focusText.Select(focusText.Text.Length, 0);
                focusText.HideCaret();
            }
            else if (focusControl is ComboBox { DropDownStyle: ComboBoxStyle.DropDown } focusCombo)
            {
                focusCombo.Select(0, 0);
                focusCombo.HideCaret();
            }
            else if (focusControl is TabControl focusTabControl)
            {
                focusTabControl.SelectedTab.Focus();
            }
        }

        /// <summary>
        /// Hides keyboard-triggered UI cues (focus rectangles and mnemonic underscores)
        /// by sending WM_CHANGEUISTATE to the form and all descendant controls.
        /// Some controls (like TabControl/WizardPages) don't properly propagate
        /// WM_CHANGEUISTATE to their children, so we send it recursively.
        /// </summary>
        private static void HideKeyboardCues(Control control)
        {
            var form = control.FindForm();
            if (form != null)
            {
                HideKeyboardCuesRecursive(form);
            }
        }

        private static void HideKeyboardCuesRecursive(Control control)
        {
            User32.SendMessage(control.Handle, User32.WinMessageType.WM_CHANGEUISTATE, User32.UISF_HIDEALL, IntPtr.Zero);
            foreach (Control child in control.Controls)
            {
                HideKeyboardCuesRecursive(child);
            }
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

            var shotPic = newShot.Take();
            if (processShot != null)
            {
                // Processing must include border cleanup if necessary, since
                // the shot-pic is not guaranteed to need a constant border
                // Execute processing on window's thread in case delegate accesses UI controls
                shotPic = activeWindow.Invoke(processShot, shotPic) as Bitmap;
                Assert.IsNotNull(shotPic);
            }
            else
            {
                // Tidy up annoying variations in screenshot border due to underlying windows
                // Only for unprocessed window screenshots
                // Floating windows only have a transparent titlebar border
                var form = activeWindow as DockableForm;
                shotPic = shotPic.CleanupBorder(form is { DockState: DockState.Floating });
            }

            if (scale.HasValue)
            {
                shotPic = new Bitmap(shotPic,
                    (int) Math.Round(shotPic.Width * scale.Value),
                    (int) Math.Round(shotPic.Height * scale.Value));
            }

            if (pathToSave != null)
            {
                ScreenCapture.SaveToFile(pathToSave, shotPic);
            }

            // CopyBitmapToClipboard(shotPic);

            return shotPic;
        }

        /// <summary>
        /// Captures a screenshot of an arbitrary screen rectangle.
        /// </summary>
        public Bitmap TakeShot(Rectangle screenRect, string pathToSave = null)
        {
            var shotPic = new RectangleShot(screenRect).Take();
            if (pathToSave != null)
                ScreenCapture.SaveToFile(pathToSave, shotPic);
            return shotPic;
        }

        /// <summary>
        /// Places a bitmap on the clipboard. Can safely be called from background threads.
        /// Clipboard operations must be performed from STA apartment threads, but background threads
        /// are typically MTA. Therefore, this function creates a new thread to do the copy.
        /// </summary>
        public static void CopyBitmapToClipboard(Bitmap bitmap)
        {
            // Have to do it this way because of the limitation on OLE access from background threads.
            var clipThread = new Thread(() => Clipboard.SetImage(bitmap));
            clipThread.SetApartmentState(ApartmentState.STA);
            clipThread.Start();
            clipThread.Join();
        }

        public MemoryStream SaveToMemory(Bitmap bmp)
        {
            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms;
        }
    }
}
