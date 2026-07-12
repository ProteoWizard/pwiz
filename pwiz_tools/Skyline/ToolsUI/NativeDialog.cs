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

        /// <summary>
        /// The wrapper that drives the "#32770" at <paramref name="handle"/>: a file-dialog subclass when it is
        /// one, otherwise a generic <see cref="NativeDialog"/>. Null when the window is not a dialog (or is gone).
        ///
        /// <para>Every test is a window-manager read (a control id, a window class), so classifying is safe from any
        /// thread and cannot deadlock -- which the UI-Automation version it replaces could, queried from the UI
        /// thread the dialog's modal loop runs on.</para>
        /// </summary>
        public static NativeDialog Create(IntPtr handle, CancellationToken cancellationToken)
        {
            if (handle == IntPtr.Zero || User32.GetClassName(handle) != DIALOG_CLASS_NAME)
                return null;
            if (NativeOpenFileDialog.IsOpenFileDialog(handle))
                return new NativeOpenFileDialog(handle, cancellationToken);
            // Check Save after Open: the modern Open dialog has the classic file-name combo (control id 1148) that
            // IsOpenFileDialog keys on; the Save dialog does not, so the two never both match.
            if (NativeSaveFileDialog.IsSaveFileDialog(handle))
                return new NativeSaveFileDialog(handle, cancellationToken);
            // The classic Browse-For-Folder dialog (a folder tree, no file-name field) -- checked after the file
            // dialogs, whose navigation pane also has a tree but which match first on their file-name field.
            if (NativeFolderBrowserDialog.IsFolderBrowserDialog(handle))
                return new NativeFolderBrowserDialog(handle, cancellationToken);
            // Any other "#32770" (a message box such as the Save dialog's "replace it?" confirm, or any other
            // Windows dialog) is driven generically by the base class -- it lists its buttons and clicks one by
            // caption.
            return new NativeDialog(handle, cancellationToken);
        }

        /// <summary>The native dialogs currently open in this process, each wrapped as the class that drives it.</summary>
        public static IList<NativeDialog> GetOpenDialogs(CancellationToken cancellationToken)
        {
            var result = new List<NativeDialog>();
            var seen = new HashSet<IntPtr>();
            foreach (var hwnd in FindDialogHandles())
            {
                // A dialog can vanish mid-enumeration -- it is closing (e.g. a message box just dismissed). Every
                // read below then simply reports the window as gone rather than throwing, but classify defensively
                // anyway: a window we cannot classify is one we cannot drive, and it is on its way out.
                var dialog = Create(hwnd, cancellationToken);
                if (dialog != null && seen.Add(hwnd))
                    result.Add(dialog);
            }
            return result;
        }

        // ---- Win32 control lookup (what UI Automation used to do) -----------------------------------

        /// <summary>The dialog's descendant windows of the given class, in window order -- the dialog's real
        /// controls. A modern file dialog's DirectUI content (navigation pane, file list, breadcrumb) is drawn
        /// rather than windowed, so it has no handles and never appears here; the controls the connector drives
        /// (the file-name field, the commit and cancel buttons) all do.</summary>
        protected IEnumerable<IntPtr> FindDescendants(string className)
        {
            return User32.EnumChildWindows(Hwnd).Where(hwnd => User32.GetClassName(hwnd) == className);
        }

        /// <summary>The dialog's descendant of the given class carrying the given control id, or IntPtr.Zero. The
        /// class matters: the Save dialog's file-name Edit and the address bar's breadcrumb BOTH carry control id
        /// 1001, but the breadcrumb is a ToolbarWindow32 -- so class + id is unambiguous where the id alone is not
        /// (which is why the UI-Automation version had to walk to a DirectUI host to disambiguate).</summary>
        protected IntPtr FindDescendant(string className, int controlId)
        {
            return FindDescendants(className)
                .FirstOrDefault(hwnd => User32.GetDlgCtrlID(hwnd) == controlId);
        }

        /// <summary>Whether the dialog has ANY descendant carrying the given control id, whatever its class -- how
        /// the Open dialog recognizes itself. Its file-name combo is a stack of three windows (a ComboBoxEx32, the
        /// ComboBox inside it, and the Edit inside that) and which of them carries the id varies by dialog flavour:
        /// the plain Open dialog puts it on all three, the multiselect one ("Add Input Files") does not put it on
        /// the Edit. So the id alone -- not id + class -- is what identifies the dialog.</summary>
        protected bool HasDescendantWithControlId(int controlId)
        {
            return User32.EnumChildWindows(Hwnd).Any(hwnd => User32.GetDlgCtrlID(hwnd) == controlId);
        }

        /// <summary>Whether the dialog has a descendant of the given class AND control id -- how the Save dialog
        /// recognizes its file-name Edit, whose id (1001) the address bar's breadcrumb also carries (but as a
        /// ToolbarWindow32, so the class separates them).</summary>
        protected bool HasDescendant(string className, int controlId)
        {
            return FindDescendant(className, controlId) != IntPtr.Zero;
        }

        /// <summary>The dialog's descendant of the given class and control id, or a clear failure. Found by ID, not
        /// by walking the visible children: a dialog is discoverable (its window exists) a moment before it is
        /// SHOWN, and until then every one of its controls reports itself invisible -- so a lookup that filtered on
        /// visibility would intermittently find nothing. The id is stable from the moment the control is created.</summary>
        protected IntPtr RequireDescendant(string className, int controlId, string description)
        {
            var hwnd = FindDescendant(className, controlId);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"The native dialog '{0}' has no {1}.", FormId, description));
            return hwnd;
        }

        /// <summary>The dialog's button with the given control id (IDOK, IDCANCEL) -- a commit button found WITHOUT
        /// matching a localized caption.</summary>
        protected NativeButton RequireButton(int controlId, string description)
        {
            return new NativeButton(RequireDescendant(NativeControl.BUTTON_CLASS, controlId, description + @" button"),
                CancellationToken);
        }

        // The dialog's controls, as elements: its VISIBLE Button / Edit / Static descendants. Visible is what
        // separates a dialog's real controls from the hidden scratch windows both file dialogs carry (the
        // collapsed address-bar Edit, a hidden Help button) -- and what a user can act on, which is the rule the
        // rest of the connector follows.
        public override IEnumerable<UiElement> EnumerateChildren()
        {
            foreach (var hwnd in User32.EnumChildWindows(Hwnd))
            {
                if (!User32.IsWindowVisible(hwnd))
                    continue;
                var element = ElementFor(hwnd);
                if (element != null)
                    yield return element;
            }
        }

        // The element for a native child window, or null for one that is not something a caller can act on or read
        // (the DirectUI hosts, the shell view, the scroll bars).
        private NativeControl ElementFor(IntPtr hwnd)
        {
            switch (User32.GetClassName(hwnd))
            {
                case NativeControl.BUTTON_CLASS:
                    return new NativeButton(hwnd, CancellationToken);
                case NativeControl.EDIT_CLASS:
                    return new NativeTextBox(hwnd, CancellationToken);
                case NativeControl.STATIC_CLASS:
                    // A Static with no text is an icon or a spacer, not a label.
                    return string.IsNullOrEmpty(User32.GetWindowText(hwnd))
                        ? null
                        : (NativeControl) new NativeLabel(hwnd, CancellationToken);
                default:
                    return null;
            }
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

        /// <summary>Accepts the dialog by pressing Enter, which activates its default button. OkDialog runs the Win32
        /// gesture on the dialog's UI thread and waits until it closes -- the SAME machinery a managed form's accept
        /// uses. Enter is POSTED, so the dialog's modal loop translates it into accept (a synchronous send would
        /// bypass that -- see PostEnter). The file dialogs override this with the gesture their surface needs.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            return OkDialog(() => PostEnter(Hwnd));
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

        // ---- Driven exactly the way JsonUiService drives a managed form ----------------------------
        // Its controls are NativeControl elements (see EnumerateChildren), so the shared element machinery --
        // GetControlsNow, FindElement, the Click/SetValue actions -- does all the work, and none of these verbs
        // special-cases a native dialog any more. Reads are Win32 and run on the calling thread; a gesture is
        // marshaled onto the dialog's UI thread and waited out, so it is counted and a modal it raises is seen.

        /// <summary>The id this dialog is addressed by (see skyline_get_open_forms): "Dialog:Open …".</summary>
        public override string FormId => DialogTypeName + @":" + Title;

        public override ControlInfo[] GetControls() => GetControlsNow();

        // Clicks the dialog's button whose caption matches -- how the connector gets past a confirm box ("Yes"), or
        // presses a file dialog's Open/Save. Resolved and clicked through the element model, so the click WAITS: it
        // reports whether it completed or left a dialog open (the Save dialog's "replace it?" prompt), where the old
        // UI-Automation version could only post the click and tell the caller to go and poll. Caption matching is
        // case- and '&'-mnemonic-insensitive (see UiElement.TextMatches).
        public override ActionResult ClickButton(string button)
        {
            var element = FindElement(button, UiActions.Click);
            return (ActionResult) UiActions.Click.Invoke(element, null);
        }

        // A native dialog takes a value only as its file name (a Save dialog, an Open dialog); the base refuses, and
        // the file dialogs override SetValueCore to type the path. The value is typed synchronously and has no
        // follow-on work, so the set is complete on return.
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

        /// <summary>
        /// The open native dialogs (window class "#32770") of the current process, by Win32 EnumWindows. A common
        /// file dialog is an *owned* top-level window (not a child window), and EnumWindows enumerates owned
        /// windows -- so this finds a dialog owned by a nested modal form (e.g. the "Add Input Files" dialog owned
        /// by the Import Peptide Search wizard). EnumWindows visits only top-level windows, never the control
        /// subtree, so it stays cheap.
        /// </summary>
        private static IEnumerable<IntPtr> FindDialogHandles()
        {
            var processId = (uint) Process.GetCurrentProcess().Id;
            return User32.EnumWindows().Where(hwnd =>
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                return windowProcessId == processId && User32.GetClassName(hwnd) == DIALOG_CLASS_NAME;
            });
        }

        protected static TResult PollUntil<TResult>(int millisTimeout, string description, Func<TResult> find)
            where TResult : class
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var value = find();
                if (value != null)
                    return value;
                if (stopwatch.ElapsedMilliseconds > millisTimeout)
                    throw new TimeoutException(string.Format(@"Timed out after {0} ms waiting for {1}.", millisTimeout, description));
                Thread.Sleep(POLL_INTERVAL_MILLIS);
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
