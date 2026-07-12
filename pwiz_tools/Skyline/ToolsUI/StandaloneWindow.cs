using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>A top-level window the connector addresses by a formId: a WinForms form
    /// (<see cref="StandaloneForm"/>) or a native common dialog (<see cref="NativeDialog"/>). JsonUiService
    /// resolves a formId to one of these and drives it entirely through this interface, so no verb
    /// special-cases a native dialog. Each implementation runs its work in its own thread context: a managed
    /// form marshals to the UI thread and can watch for a dialog the work pops; a native dialog -- whose UI
    /// thread is busy in its own modal loop -- runs on the calling (pipe) thread and cannot be watched.</summary>
    public abstract class StandaloneWindow : UiElement
    {
        protected StandaloneWindow(CancellationToken cancellationToken, IntPtr hwnd) : base(cancellationToken)
        {
            Hwnd = hwnd;
        }
        /// <summary>The "TypeName:Title" id this form is addressed by (matches skyline_get_open_forms).</summary>
        public abstract string FormId { get; }
        /// <summary>The form's visible title, for naming a captured-image file.</summary>
        public abstract string Title { get; }
        /// <summary>The form's controls as ControlInfo, each path parented onto the form (the get_controls verb).</summary>
        public abstract ControlInfo[] GetControls();
        /// <summary>Clicks a control on the form by its visible label, returning whether the click completed or
        /// left a dialog open. (To confirm a form or dialog use the accept action, and to dismiss it use Close --
        /// neither keys on a localized button caption.)</summary>
        public abstract ActionResult ClickButton(string button);
        /// <summary>Sets a control's value (or a grid cell, or a native dialog's file name), returning whether the
        /// set completed or left a dialog open.</summary>
        public abstract ActionResult SetValue(string controlId, string value);
        /// <summary>Dismisses the form/dialog by clicking the button with the given caption, then waits until it has
        /// closed and reports whether it completed. For a choice that is neither the default nor the cancel button
        /// (e.g. "No" on a "replace it?" message box). A native file dialog has no caption-addressable button, so it
        /// throws -- accept it with <see cref="DismissWithAcceptButton"/>.</summary>
        public abstract ActionResult DismissWithButton(string button);
        /// <summary>Accepts the form/dialog -- the equivalent of pressing its default button (a managed form clicks
        /// its AcceptButton, a native dialog does its OK gesture), so confirming never keys on a localized caption --
        /// then waits until it has closed and reports whether it completed.</summary>
        public abstract ActionResult DismissWithAcceptButton();
        /// <summary>Cancels the form/dialog -- presses its cancel button (or closes it when it has none) -- then
        /// waits until it has closed and reports whether it completed. The dismissing counterpart of
        /// <see cref="DismissWithAcceptButton"/>.</summary>
        public abstract ActionResult DismissWithCancelButton();
        /// <summary>Resolves the path against this form and performs the action in the form's thread context.</summary>
        public abstract object PerformAction(UiElementPath path, UiAction action, object value);
        /// <summary>Captures the form's image to a bitmap the caller disposes (no permission/format checks --
        /// the caller has done the screen-capture pre-flight).</summary>
        public abstract System.Drawing.Bitmap CaptureImage();

        // ---- Window-state queries the modal-watch (UiServiceDispatcher) asks each form about itself ----

        /// <summary>Whether this is an interactive modal the caller would drive (a managed non-progress modal --
        /// Modal and not a LongWaitDlg -- or a native dialog) rather than a progress/wait dialog the work itself
        /// drives (a LongWaitDlg), which the watch rides through instead of stopping on.</summary>
        public abstract bool IsInteractiveModal { get; }
        /// <summary>Whether the form/dialog is still on screen (a managed form: not disposed, handle created and
        /// visible; a native dialog: its window is visible). Must be read on the owning UI thread for a managed
        /// form. Its negation is "dismissed".</summary>
        public abstract bool IsOpen { get; }
        /// <summary>Whether this is a busy progress form (an <see cref="ILongWaitForm"/> mid-operation) the
        /// message-loop watchdog rides through; false for a native dialog.</summary>
        public abstract bool IsBusy { get; }
        /// <summary>The detailed message this form shows when it blocks the UI: a managed form's composed alert
        /// text (CommonFormEx.DetailedMessage) else its caption; a native dialog's message-body text else its
        /// caption. Read on the owning UI thread for a managed form.</summary>
        public abstract string DetailedMessage { get; }
        /// <summary>This form's/dialog's top-level window handle. Captured up front (a managed form reads it on
        /// its UI thread when the element is built; a native dialog is identified by it), so off-thread code such
        /// as the modal-watch can identify or capture the window without reading Form.Handle off its UI thread
        /// (which would trip the cross-thread check).</summary>
        public IntPtr Hwnd { get; }

        protected ActionResult OkDialog(Action action)
        {
            return DialogWatcher.OkDialog(Hwnd, action, CancellationToken);
        }

        protected T CallFunction<T>(Func<T> func)
        {
            return DialogWatcher.CallFunction(Hwnd, func, CancellationToken);
        }

        protected ActionResult PerformAction(Action action)
        {
            return DialogWatcher.PerformAction(Hwnd, action, CancellationToken);
        }

        public virtual string GetFormValue(string controlId)
        {
            return CallFunction(() => FindElement(controlId, UiActions.GetValue).Value?.ToString());
        }

        public virtual string[] GetOptions(string controlId)
        {
            return CallFunction(() =>
                ((IOptionsElement)FindElement(controlId, UiActions.GetOptions)).GetOptions().ToArray());
        }

        /// <summary>Clicks an item on a control's right-click menu. The control is addressed the way get_controls
        /// addresses one (by its <paramref name="controlSelector"/> label or type), or -- when that is empty -- the
        /// form's own context menu (a graph's, say, which no named child owns). <paramref name="itemText"/> is the
        /// item's visible text, or a '&gt;'-separated path into a submenu. Resolving and clicking both happen on the
        /// form's UI thread, inside the watched action: building a context menu has side effects, and an item that
        /// opens a modal blocks there, so the wait reports it rather than hanging.</summary>
        public ActionResult InvokeContextMenuItem(string controlSelector, string itemText)
        {
            if (string.IsNullOrEmpty(itemText))
                throw new ArgumentException(new LlmInstruction(@"An item text is required."));
            return PerformAction(() =>
            {
                UiElementPath itemPath = new UiElementPath(null, null, null, ContextMenuElement.TypeName);
                foreach (var segment in itemText.Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries))
                    itemPath = new UiElementPath(itemPath, segment.Trim(), null, null);
                var control = string.IsNullOrEmpty(controlSelector)
                    ? this
                    : FindElement(controlSelector, UiActions.GetChildren);
                // Not "as IClickableElement ... !": an item that does not exist, or one that cannot be clicked, must
                // say so. RequireAction throws the same message the other verbs do; a null-forgiving cast would throw
                // a NullReferenceException out of this posted action instead, surfacing as an "Unexpected Error".
                var menuItem = JsonUiService.RequireAction(control.GetDescendant(itemPath), UiActions.Click);
                ((IClickableElement) menuItem).ClickNow();
            });
        }

        /// <summary>Every top-level window of this process that is a managed Form (visible) or a native modal dialog,
        /// each wrapped as the connector window abstraction that drives it. Enumerated purely from Win32 + a
        /// Control.FromHandle lookup, so it is safe on any thread. Top-level only -- a form docked inside the main
        /// window is a child window and does not appear here (JsonUiService.GetOpenFormElements adds those).</summary>
        public static IEnumerable<StandaloneWindow> GetTopLevelWindows(CancellationToken cancellationToken)
        {
            var processId = (uint)Process.GetCurrentProcess().Id;
            foreach (var hwnd in User32.EnumWindows())
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId != processId)
                    continue;
                if (Control.FromHandle(hwnd) is Form form)
                {
                    if (User32.IsWindowVisible(hwnd))
                        yield return new StandaloneForm(form, hwnd, cancellationToken);
                }
                else yield return NativeDialog.MakeNativeDialog(hwnd, cancellationToken);
            }
        }

        // Wraps a top-level window handle as the connector form abstraction that drives it: Control.FromHandle
        // resolves a managed WinForms form (a FormElement built with the handle already in hand -- safe off the UI
        // thread), or returns nothing for a truly native window (a generic NativeDialog).
        internal static StandaloneWindow NewStandaloneWindow(IntPtr hwnd, CancellationToken cancellationToken)
        {
            return Control.FromHandle(hwnd) is Form form
                ? (StandaloneWindow)new StandaloneForm(form, hwnd, cancellationToken)
                : NativeDialog.MakeNativeDialog(hwnd, cancellationToken);
        }

        /// <summary>The handles of this process's modal dialog windows -- visible, enabled, top-level windows whose
        /// owner window is disabled (the signature of a modal blocking its owner), managed OR native. Pure Win32,
        /// so it is safe off the UI thread (the connector's cheap "did a new modal appear" check).</summary>
        private static IList<IntPtr> EnumModalWindowHandles()
        {
            var processId = (uint)Process.GetCurrentProcess().Id;
            return User32.EnumWindows().Where(hwnd =>
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                return windowProcessId == processId && IsModalDialogWindow(hwnd);
            }).ToList();
        }

        /// <summary>Every modal dialog currently open in this process, each wrapped as the connector window abstraction
        /// that drives it. A single Win32 enumeration (<see cref="EnumModalWindowHandles"/>) is the sole source, so
        /// managed and native modals are discovered the same way (see <see cref="NewStandaloneWindow"/>). Going
        /// handle -> Form avoids reading Form.Handle (which trips the cross-thread check) and needs no Form/handle
        /// pairing, so it is safe off the UI thread -- both the enumeration and Control.FromHandle are a lookup,
        /// never a UI touch.</summary>
        public static IEnumerable<StandaloneWindow> GetModalDialogs(CancellationToken cancellationToken)
        {
            return EnumModalWindowHandles().Select(hwnd => NewStandaloneWindow(hwnd, cancellationToken));
        }

        /// <summary>Whether the window is a modal dialog blocking its owner -- visible and enabled, with an owner
        /// window that is disabled (the signature of a modal). Pure Win32, so it is safe off the UI thread.</summary>
        private static bool IsModalDialogWindow(IntPtr hwnd)
        {
            if (!User32.IsWindowVisible(hwnd) || !User32.IsWindowEnabled(hwnd))
                return false;
            var owner = User32.GetOwner(hwnd);
            return owner != IntPtr.Zero && !User32.IsWindowEnabled(owner);
        }

    }
}
