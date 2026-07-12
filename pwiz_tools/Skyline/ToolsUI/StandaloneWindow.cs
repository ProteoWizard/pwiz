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
        // ---- Driving the window: the same for a managed form and a native dialog ---------------------
        //
        // These used to be abstract, implemented twice. They are not: both kinds expose their contents the same way
        // (EnumerateChildren -> UiElement.FindElement), and both act through the same UiActions -- so "click the
        // button with this caption" is one method, not two. The only thing that differed was the marshaling, and
        // that is now handled by the window itself: InvokeOnUiThread dispatches to a managed form's own thread, and
        // for a native dialog it runs inline (its reads are Win32, safe on any thread). So the code below reads the
        // same and does the right thing for both.

        /// <summary>The form's controls as ControlInfo, each path parented onto the form (the get_controls verb) --
        /// which is exactly the get_children action performed on the window itself.</summary>
        public ControlInfo[] GetControls()
        {
            return UiActions.GetChildren.Call(this);
        }

        /// <summary>Clicks a control on the form by its visible label, returning whether the click completed or left
        /// a dialog open. The control is resolved on the window's own thread (so a missing one fails HERE, with a
        /// message naming what it looked for), then clicked through the Click action, which posts the gesture and
        /// waits it out. (To confirm or dismiss a form or dialog use the dismiss action, or the DismissWith... verbs
        /// -- none of which keys on a localized button caption.)</summary>
        public ActionResult ClickButton(string button)
        {
            var element = InvokeOnUiThread(() => FindElement(button, UiActions.Click));
            return (ActionResult) UiActions.Click.Invoke(element, null);
        }

        /// <summary>Sets a control's value (or a grid cell, or a native dialog's file name), returning whether the
        /// set completed or left a dialog open. The one verb that genuinely differs: a managed form addresses a
        /// control (or a grid cell) by id, a native dialog has only its file-name field and ignores the id.</summary>
        public abstract ActionResult SetValue(string controlId, string value);

        /// <summary>Dismisses the form/dialog by clicking the button with the given caption, then waits until it has
        /// closed and reports whether it completed. For a choice that is neither the default nor the cancel button
        /// (e.g. "No" on a "replace it?" message box). The click is posted; the empty-action OkDialog then rides the
        /// shared wait until the window has closed and the nesting count has drained.</summary>
        public ActionResult DismissWithButton(string button)
        {
            ClickButton(button);
            return OkDialog(() => { });
        }
        /// <summary>Accepts the form/dialog -- the equivalent of pressing its default button (a managed form clicks
        /// its AcceptButton, a native dialog does its OK gesture), so confirming never keys on a localized caption --
        /// then waits until it has closed and reports whether it completed.</summary>
        public abstract ActionResult DismissWithAcceptButton();
        /// <summary>Cancels the form/dialog -- presses its cancel button (or closes it when it has none) -- then
        /// waits until it has closed and reports whether it completed. The dismissing counterpart of
        /// <see cref="DismissWithAcceptButton"/>.</summary>
        public abstract ActionResult DismissWithCancelButton();
        /// <summary>Resolves the path against this window and performs the action. Resolving and gating happen on the
        /// window's own thread (a control's gates read window handles); the action then supplies its own threading --
        /// a gesture posts itself and waits it out, a read runs inside the dialog-watch -- so perform_action behaves
        /// exactly like the named verbs above, which go through the same actions.</summary>
        public object PerformAction(UiElementPath path, UiAction action, object value)
        {
            var element = InvokeOnUiThread(() =>
                JsonUiService.RequireAction(JsonUiService.ResolvePath(path, this), action));
            return action.Invoke(element, value);
        }
        /// <summary>Captures the form's image to a bitmap the caller disposes (no permission/format checks --
        /// the caller has done the screen-capture pre-flight).</summary>
        public abstract System.Drawing.Bitmap CaptureImage();

        // ---- Window-state queries the modal-watch (UiServiceDispatcher) asks each form about itself ----

        /// <summary>Whether the window is modal -- it blocks the window that opened it until it goes away.</summary>
        public abstract bool IsModal { get; }

        /// <summary>Whether the window will go away BY ITSELF, without anyone dismissing it -- a LongWaitDlg, which
        /// closes when the work it is reporting on finishes. Nothing else does: every other form stands there until
        /// it is dismissed.
        ///
        /// <para>This is the distinction the connector's wait turns on. A modal that is NOT transient
        /// (<c>IsModal &amp;&amp; !IsTransient</c>) is a stop: the wait reports it and hands control straight back to
        /// the caller, who must drive it. A transient one is ridden through -- waiting for it is the right thing to
        /// do, because it will finish on its own.</para></summary>
        public abstract bool IsTransient { get; }
        /// <summary>Whether the form/dialog is still on screen (a managed form: not disposed, handle created and
        /// visible; a native dialog: its window is visible). Must be read on the owning UI thread for a managed
        /// form. Its negation is "dismissed".</summary>
        public abstract bool IsOpen { get; }
        /// <summary>Whether this is a progress form actively reporting work in flight (an
        /// <see cref="ILongWaitForm"/> mid-operation). It is what tells the message-loop watchdog that a wait is
        /// getting somewhere -- work IS advancing -- rather than stuck; false for a native dialog.</summary>
        public abstract bool IsProgressing { get; }
        /// <summary>The detailed message this form shows when it blocks the UI: a managed form's composed alert
        /// text (CommonFormEx.DetailedMessage) else its caption; a native dialog's message-body text else its
        /// caption. Read on the owning UI thread for a managed form.</summary>
        public abstract string DetailedMessage { get; }
        /// <summary>This form's/dialog's top-level window handle. Captured up front (a managed form reads it on
        /// its UI thread when the element is built; a native dialog is identified by it), so off-thread code such
        /// as the modal-watch can identify or capture the window without reading Form.Handle off its UI thread
        /// (which would trip the cross-thread check).</summary>
        public IntPtr Hwnd { get; }

        // A top-level window IS its own form, so the window a gesture on it is marshaled through is itself.
        internal override IntPtr FormHwnd => Hwnd;

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

        // Both reads resolve the control and read it in ONE trip to the form's thread (CallFunction puts us there),
        // so the raw CallNow is what to use -- not Call, which would marshal a second time. Going through the action
        // rather than the element's method directly means the named verb returns exactly what the generic get_value /
        // get_options return, and needs no cast to the capability interface to do it.
        public virtual string GetFormValue(string controlId)
        {
            return CallFunction(() =>
                UiActions.GetValue.CallNow(FindElement(controlId, UiActions.GetValue))?.ToString());
        }

        public virtual string[] GetOptions(string controlId)
        {
            return CallFunction(() =>
                UiActions.GetOptions.CallNow(FindElement(controlId, UiActions.GetOptions)));
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
                if (Control.FromHandle(hwnd) is Form)
                {
                    if (User32.IsWindowVisible(hwnd))
                        yield return NewStandaloneWindow(hwnd, cancellationToken);
                }
                else yield return NativeDialog.MakeNativeDialog(hwnd, cancellationToken);
            }
        }

        // Wraps a top-level window handle as the connector form abstraction that drives it: Control.FromHandle
        // resolves a managed WinForms form (a FormElement built with the handle already in hand -- safe off the UI
        // thread), or returns nothing for a truly native window (a generic NativeDialog).
        internal static StandaloneWindow NewStandaloneWindow(IntPtr hwnd, CancellationToken cancellationToken)
        {
            switch (Control.FromHandle(hwnd))
            {
                case Form form:
                    return StandaloneForm.Create(form, hwnd, cancellationToken);
                case { } control:
                    throw new ArgumentException(new LlmInstruction($@"{control.GetType().Name} is not a form"));
                default:
                    return NativeDialog.MakeNativeDialog(hwnd, cancellationToken);
            }
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
