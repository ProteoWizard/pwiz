/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base class for driving a single native Windows dialog (window class "#32770", such as the
    /// common Open/Save file dialog) using UI Automation. An instance wraps one open dialog,
    /// identified by its window handle. A native dialog is not a WinForms form, so it does not
    /// appear in FormUtil.OpenForms and cannot be introspected or driven the way the rest of
    /// Skyline's UI is; UI Automation reaches it the same way a user would.
    ///
    /// This is shared product code with two consumers: the functional test framework
    /// (AbstractFunctionalTest.OpenDocument) and the Skyline MCP server's UI interaction layer.
    ///
    /// A native dialog is modal and runs its own message loop on the Skyline UI thread, so the
    /// action that displays it does not return until the dialog closes and must therefore be posted
    /// to the UI thread (e.g. with BeginInvoke). While the dialog is up the UI thread keeps pumping
    /// that modal loop, so messages and posted actions still run there. The methods here drive an
    /// already-open dialog by posting Win32 messages and through UI Automation, so they do not care
    /// which thread they are called from -- a consumer may run them on a background thread (the test
    /// thread or the MCP pipe thread) or post them back onto the UI thread, whose modal loop pumps
    /// them.
    ///
    /// Use <see cref="WaitForDialog{T}"/> or <see cref="GetOpenDialogs"/> to obtain an instance;
    /// <see cref="Create"/> chooses the subclass that matches a given dialog element.
    ///
    /// A dialog is a <see cref="UiElement"/>: it presents to the connector exactly like any other form (it
    /// is listed by GetOpenForms and addressed by a path whose Text is its id). This base class is concrete
    /// and drives any "#32770" generically: it lists the dialog's text and push buttons, clicks a button by
    /// its caption, accepts by pressing the default button, and cancels by sending WM_CLOSE -- enough
    /// for a message box (e.g. the Save dialog's "replace it?" confirm) or any other Windows dialog. The
    /// file dialogs (<see cref="NativeFileDialog"/>) specialize it with the file-name field: set_value types
    /// a path and the accept gesture differs by surface.
    /// </summary>
    public class NativeDialog : StandaloneWindow
    {
        protected const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class
        private const int DEFAULT_TIMEOUT_MILLIS = 30 * 1000;
        private const int POLL_INTERVAL_MILLIS = 100;
        private const int VK_RETURN = 0x0D;

        private AutomationElement _dialogElement;

        protected NativeDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(cancellationToken, windowHandle)
        {
        }

        /// <summary>How long the wait helpers poll for a control before throwing.</summary>
        public int MillisTimeout { get; set; } = DEFAULT_TIMEOUT_MILLIS;

        /// <summary>
        /// Short identifier for the kind of dialog, used as the first half of <see cref="FormId"/>. It is always
        /// "Dialog": a subclass overrides this ONLY for a kind that can be told the instant the window appears,
        /// before any of its children exist. The file/folder dialogs cannot -- classifying them needs their
        /// (lazily-created) DirectUI children -- so they keep "Dialog". Baking a lagging classification into the id
        /// would make it unstable: the connector reports the modal the moment it is shown (a file dialog then still
        /// looks like a generic "Dialog"), and a later ResolveForm would see a different id. Callers tell a native
        /// dialog apart by <see cref="FormInfo.IsNative"/> and its caption, not this.
        /// </summary>
        public virtual string DialogTypeName => @"Dialog";

        /// <summary>The dialog window's caption.</summary>
        public override string Title => User32.GetWindowText(Hwnd);

        // UiElement: a native dialog is the root of its own path, so most of these are not used for matching
        // (the dialog is found by its form id, not walked into as a child). They are implemented so the
        // dialog is a first-class element whose children can be listed and acted on like any form's.
        public override string Name => string.Empty;
        public override Type ElementType => GetType();
        public override bool IsEnabled => User32.IsWindowEnabled(Hwnd);

        // ---- Window-state queries (IFormElement) the modal-watch asks of a native dialog, all via Win32 (never
        // UI Automation, which can deadlock when queried from the UI thread the dialog's modal loop is running on).
        /// <summary>A native dialog is always an interactive stop the caller would drive (never a progress dialog).</summary>
        public override bool IsInteractiveModal => true;
        /// <summary>Whether the dialog window is still visible.</summary>
        public override bool IsOpen => User32.IsWindowVisible(Hwnd);
        /// <summary>A native dialog is never an ILongWaitForm busy progress form.</summary>
        public override bool IsBusy => false;
        /// <summary>The dialog's message body (its Static-control text) else its caption.</summary>
        public override string DetailedMessage => NativeBodyText(Hwnd) ?? User32.GetWindowText(Hwnd);

        /// <summary>Wraps a raw modal window handle (one with no managed Form) as a generic native dialog, for the
        /// modal-watch's Win32-only queries. Not a UI-Automation-driven file dialog -- see <see cref="Create"/>.</summary>
        public static NativeDialog MakeNativeDialog(IntPtr handle, CancellationToken cancellationToken) =>
            new NativeDialog(handle, cancellationToken);

        // The message body of a native dialog box (a Win32 #32770, e.g. MessageBox.Show), read from its child
        // controls, or null when none is found. The body is a "Static" control with text -- the icon's Static has
        // none -- so among the Static children with non-empty text, take the LONGEST, so the message wins over any
        // short label. In-process, so GetWindowText reads the static's text directly.
        private static string NativeBodyText(IntPtr hwnd)
        {
            return User32.EnumChildWindows(hwnd)
                .Where(child => User32.GetClassName(child) == @"Static")
                .Select(User32.GetWindowText)
                .Where(text => !string.IsNullOrEmpty(text))
                .OrderByDescending(text => text.Length)
                .FirstOrDefault();
        }

        protected AutomationElement DialogElement =>
            _dialogElement ??= AutomationElement.FromHandle(Hwnd);

        /// <summary>
        /// Returns the automation wrapper for the given native dialog element: a file-dialog subclass when
        /// it is one, otherwise a generic <see cref="NativeDialog"/>. Null only when the element is not a
        /// "#32770" dialog (or its handle is gone).
        /// </summary>
        public static NativeDialog Create(AutomationElement dialog, CancellationToken cancellationToken)
        {
            IntPtr handle;
            try
            {
                if (dialog.Current.ClassName != DIALOG_CLASS_NAME)
                    return null;
                handle = new IntPtr(dialog.Current.NativeWindowHandle);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            if (handle == IntPtr.Zero)
                return null;
            if (NativeOpenFileDialog.IsOpenFileDialog(dialog))
                return new NativeOpenFileDialog(handle, cancellationToken);
            // Check Save after Open: the modern Open dialog has the classic file-name combo
            // (AutomationId 1148) that IsOpenFileDialog keys on; the Save dialog does not, so the
            // two never both match.
            if (NativeSaveFileDialog.IsSaveFileDialog(dialog))
                return new NativeSaveFileDialog(handle, cancellationToken);
            // The classic Browse-For-Folder dialog (a folder tree, no file-name combo) -- checked after the
            // file dialogs, whose navigation pane also has a tree but which match first on their combo.
            if (NativeFolderBrowserDialog.IsFolderBrowserDialog(dialog))
                return new NativeFolderBrowserDialog(handle, cancellationToken);
            // Any other "#32770" (a message box such as the Save dialog's "replace it?" confirm, or any
            // other Windows dialog) is driven generically by the base class -- it lists the buttons and
            // clicks one by caption.
            return new NativeDialog(handle, cancellationToken);
        }

        /// <summary>
        /// Returns automation wrappers for the native dialogs currently open in this process,
        /// found via UI Automation.
        /// </summary>
        public static IList<NativeDialog> GetOpenDialogs(CancellationToken cancellationToken)
        {
            var result = new List<NativeDialog>();
            var seen = new HashSet<IntPtr>();
            foreach (var element in FindDialogElements())
            {
                NativeDialog automation;
                try
                {
                    automation = Create(element, cancellationToken);
                }
                catch (Exception)
                {
                    // A dialog can vanish mid-enumeration -- it is closing (e.g. a message box just dismissed) --
                    // and classifying it (Create's UI-Automation queries) then throws. Skip it: a window we cannot
                    // even classify is one we cannot drive, and it is on its way out. Without this, a caller polling
                    // GetOpenForms while a dialog closes would see the whole call throw.
                    continue;
                }
                if (automation != null && automation.Hwnd != IntPtr.Zero && seen.Add(automation.Hwnd))
                    result.Add(automation);
            }
            return result;
        }

        /// <summary>
        /// Waits for a native dialog of the given type to appear in this process and returns its
        /// automation wrapper.
        /// </summary>
        public static T WaitForDialog<T>(int millisTimeout = DEFAULT_TIMEOUT_MILLIS,
            CancellationToken cancellationToken = default(CancellationToken)) where T : NativeDialog
        {
            return PollUntil(millisTimeout, typeof(T).Name,
                () => GetOpenDialogs(cancellationToken).OfType<T>().FirstOrDefault());
        }

        /// <summary>Dismisses the dialog by clicking the button with the given caption (found among the dialog's
        /// child buttons), then waits until the dialog has closed -- e.g. "No" on a "replace it?" message box. A file
        /// dialog's commit button is DirectUI (not a child button), so this throws "no button" for one; accept a file
        /// dialog with <see cref="DismissWithAcceptButton"/> instead. Must be called off the UI thread.</summary>
        public override ActionResult DismissWithButton(string button)
        {
            ClickButton(button);
            return OkDialog(() => { });
        }

        /// <summary>Accepts the dialog by pressing Enter, which activates its default button. Its target control is
        /// resolved HERE, on the CALLER (pipe) thread, where UI Automation is safe (off the dialog's own UI thread);
        /// DialogWatcher.OkDialog then runs the Win32 gesture on the dialog's UI thread and waits until it closes --
        /// the SAME machinery a managed form's accept uses. Enter is POSTED, so the dialog's modal loop translates it
        /// into accept (a synchronous send would bypass that -- see PostEnter). The file dialogs override this with
        /// the gesture their DirectUI surface needs. Must be called off the UI thread.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            var handle = new IntPtr(DialogElement.Current.NativeWindowHandle);
            return OkDialog(() => PostEnter(handle));
        }

        /// <summary>Cancels the dialog by sending WM_CLOSE on its own UI thread (which dismisses it the way the
        /// title-bar close button would -- more reliable than invoking the Cancel button through UI Automation on a
        /// DirectUI surface), riding the shared wait until it closes. A message box with no cancel/close affordance
        /// (a Yes/No box) ignores WM_CLOSE -- dismiss such a box with <see cref="DismissWithButton"/>. Must be called
        /// off the UI thread.</summary>
        public override ActionResult DismissWithCancelButton()
        {
            return OkDialog(() => User32.SendMessage(Hwnd, User32.WinMessageType.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
        }

        // ---- IFormElement: drive the dialog the way JsonUiService drives any form -------------------
        // Everything runs on the calling (pipe) thread: the dialog runs its own modal loop on the UI thread,
        // so marshalling to it (or into RunWithDialogWatch's background work) would deadlock. The dialog's
        // gestures post Win32 messages / drive UI Automation, which are safe from any thread.

        /// <summary>The id this dialog is addressed by (see skyline_get_open_forms): "FileDialog:Open …".</summary>
        public override string FormId => DialogTypeName + @":" + Title;
            
        public override ControlInfo[] GetControls() => GetControlsNow();

        // The dialog's text and its push buttons, so a caller can read the prompt and see which choices it
        // offers. The buttons are clicked by caption (ClickFormButton) or with the accept/close actions; the
        // text rows are informational. (A file dialog's modern controls are DirectUI and mostly not direct
        // children, so this is typically empty for one -- it is driven by set_value + accept instead.)
        public override ControlInfo[] GetControlsNow()
        {
            var result = new List<ControlInfo>();
            foreach (var text in FindChildren(ControlType.Text))
            {
                var content = text.Current.Name;
                if (!string.IsNullOrEmpty(content))
                    result.Add(new ControlInfo { Path = new UiElementPath(null, content, null, @"Text") });
            }
            foreach (var button in FindChildren(ControlType.Button))
                result.Add(new ControlInfo
                {
                    Path = new UiElementPath(null, button.Current.Name, null, @"Button"),
                    Enabled = button.Current.IsEnabled,
                });
            return result.ToArray();
        }

        // Clicks the dialog's button whose caption matches -- e.g. how the connector gets past a confirm box
        // ("Yes"). Posts BM_CLICK (does not send it): the click may dismiss the dialog and unwind a nested
        // modal loop, so a synchronous send could wedge the caller (see NativeSaveFileDialog.Accept). Caption
        // matching is case- and '&'-mnemonic-insensitive; the caller reads the localized caption from
        // get_controls.
        public override ActionResult ClickButton(string button)
        {
            var buttons = FindChildren(ControlType.Button).ToList();
            var match = buttons.FirstOrDefault(b => CaptionMatches(b.Current.Name, button));
            if (match == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"The dialog '{0}' has no button '{1}'. Its buttons are: {2}.",
                    FormId, button, string.Join(@", ", buttons.Select(b => b.Current.Name))));
            // Capture the button handle and the dialog id BEFORE posting: the click may close the dialog (a
            // message box's OK/Yes/No does), after which its UI-Automation element is gone and FormId (Title) would
            // throw ElementNotAvailableException.
            var buttonHandle = new IntPtr(match.Current.NativeWindowHandle);
            var formId = FormId;
            User32.PostMessageA(buttonHandle, User32.WinMessageType.BM_CLICK, 0, 0);
            // The click is posted (not waited on): a native dialog is not part of the modal-nesting count, so
            // completion cannot be confirmed. Report it as not completed with a note to poll.
            return new ActionResult
            {
                Completed = false,
                Message = LlmInstruction.Format(
                    @"Posted the click of '{0}' to native dialog '{1}'; poll skyline_get_open_forms for the result.",
                    button, formId)
            };
        }

        // A native dialog takes a value only as its file name (a Save dialog, an Open dialog); the base
        // refuses, and the file dialogs override SetValueCore to type the path. The value is typed synchronously
        // and has no follow-on work, so the set is complete on return.
        public override ActionResult SetValue(string controlId, string value)
        {
            VerifyNotBlocked();
            SetValueCore(value);
            return new ActionResult { Completed = true };
        }

        protected virtual void SetValueCore(string value)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"Setting values is not supported for native dialog {0}.", FormId));
        }

        public override object PerformAction(UiElementPath path, UiAction action, object value)
        {
            var element = JsonUiService.RequireAction(JsonUiService.ResolvePath(path, this), action);
            return action.Invoke(element, value);
        }

        public override System.Drawing.Bitmap CaptureImage() => JsonUiService.CaptureNativeWindow(Hwnd);

        private void VerifyNotBlocked()
        {
            if (!IsEnabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Cannot interact with native dialog '{0}' because it is blocked.", FormId));
        }

        protected void BringToForeground()
        {
            User32.SetForegroundWindow(Hwnd);
        }

        /// <summary>
        /// Accepts the dialog by posting Enter to the given control. Posting the key is more
        /// reliable than invoking the default button through UI Automation (the dialog is a
        /// DirectUI surface).
        /// </summary>
        protected void PressEnter(AutomationElement element)
        {
            PostEnter(new IntPtr(element.Current.NativeWindowHandle));
        }

        // Posts Enter (key down then up) to a control. Enter MUST be POSTED, not sent: the dialog's modal message
        // loop translates a queued VK_RETURN into its default action (IsDialogMessage) -- a synchronous send, calling
        // the control's wndproc directly, bypasses that and does not accept the dialog. (Unlike BM_CLICK, which acts
        // on the button regardless of how it arrives; see SendClick.) The post is thread-agnostic, so a gesture posted
        // from the UI thread lands on the same queue and is accepted when OkDialog's wait next flushes it.
        protected static void PostEnter(IntPtr handle)
        {
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYDOWN, VK_RETURN, 0);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYUP, VK_RETURN, 0);
        }

        // Sends BM_CLICK to a button synchronously. Run on the button's OWN UI thread (an Accept override passes this
        // to OkDialog), so a click that opens a nested modal blocks there (keeping its action counted) rather than
        // pinning the pipe thread. BM_CLICK acts on the button directly, so -- unlike an Enter keypress (see
        // PostEnter) -- it needs no dialog-message-loop translation and works when sent.
        protected static void SendClick(IntPtr buttonHandle)
        {
            User32.SendMessage(buttonHandle, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>Waits for a descendant control of the dialog with the given AutomationId.</summary>
        protected AutomationElement WaitForElement(string automationId)
        {
            return WaitForElement(
                new PropertyCondition(AutomationElement.AutomationIdProperty, automationId),
                @"dialog control with AutomationId " + automationId);
        }

        /// <summary>Waits for a descendant control of the dialog matching the given condition.</summary>
        protected AutomationElement WaitForElement(Condition condition, string description)
        {
            return PollUntil(MillisTimeout, description,
                () => DialogElement.FindFirst(TreeScope.Descendants, condition));
        }

        /// <summary>
        /// Finds the open native dialogs (window class "#32770") of the current process by
        /// enumerating top-level windows with Win32 EnumWindows. A common file dialog is an *owned*
        /// top-level window (not a child window), and EnumWindows enumerates owned windows -- so this
        /// finds a dialog owned by a nested modal form (e.g. the "Add Input Files" dialog owned by
        /// the Import Peptide Search wizard), which a UI Automation child walk from the desktop root
        /// misses because such a dialog is nested below its owner in the automation tree. EnumWindows
        /// visits only top-level windows, never the control subtree, so it stays cheap.
        /// </summary>
        private static IList<AutomationElement> FindDialogElements()
        {
            var processId = (uint)Process.GetCurrentProcess().Id;
            var dialogHandles = User32.EnumWindows().Where(hwnd =>
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                return windowProcessId == processId && GetWindowClassName(hwnd) == DIALOG_CLASS_NAME;
            }).ToList();

            var result = new List<AutomationElement>();
            foreach (var hwnd in dialogHandles)
            {
                try
                {
                    var element = AutomationElement.FromHandle(hwnd);
                    if (element != null)
                        result.Add(element);
                }
                catch (ElementNotAvailableException)
                {
                    // Window closed between enumeration and binding; skip it.
                }
                catch (ArgumentException)
                {
                    // Handle no longer valid; skip it.
                }
            }
            return result;
        }

        private static string GetWindowClassName(IntPtr hwnd) => User32.GetClassName(hwnd);

        private static TResult PollUntil<TResult>(int millisTimeout, string description, Func<TResult> find)
            where TResult : class
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                // UI Automation can briefly throw while a window is being created or torn down.
                TResult value = null;
                try
                {
                    value = find();
                }
                catch (ElementNotAvailableException)
                {
                    // Retry below.
                }
                if (value != null)
                    return value;
                if (stopwatch.ElapsedMilliseconds > millisTimeout)
                    throw new TimeoutException(string.Format(@"Timed out after {0} ms waiting for {1}.", millisTimeout, description));
                Thread.Sleep(POLL_INTERVAL_MILLIS);
            }
        }

        // The dialog's direct children of the given control type (its buttons, its text). A message box's
        // buttons and text are direct children of the "#32770"; a modern file dialog's are DirectUI and
        // nested, so this returns few or none for one.
        private IEnumerable<AutomationElement> FindChildren(ControlType controlType)
        {
            try
            {
                return DialogElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)).Cast<AutomationElement>();
            }
            catch (ElementNotAvailableException)
            {
                return Enumerable.Empty<AutomationElement>();
            }
        }

        // A button caption matches the requested text ignoring case and the '&' mnemonic marker.
        private static bool CaptionMatches(string actual, string requested)
        {
            return string.Equals(StripMnemonic(actual), StripMnemonic(requested), StringComparison.OrdinalIgnoreCase);
        }

        private static string StripMnemonic(string text)
        {
            return (text ?? string.Empty).Replace(@"&", string.Empty).Trim();
        }
    }
}
