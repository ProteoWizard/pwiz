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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Base class for tutorial tests that drive Skyline through the in-process <see cref="IJsonToolService"/>
    /// -- the same API an external MCP client uses -- rather than directly through ShowDialog / RunUI. Besides
    /// reproducing a tutorial, such a test verifies that the connector is capable enough to run the tutorial
    /// end-to-end; a step that cannot be expressed through the connector is a gap to add to it.
    /// </summary>
    public abstract class McpTutorialTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// The running JSON tool service -- the same <see cref="IJsonToolService"/> an external MCP client
        /// drives. Call <see cref="StartToolService"/> once (e.g. at the top of DoTest) before using it.
        /// </summary>
        protected IJsonToolService Connector => Program.MainJsonToolServer;

        /// <summary>
        /// Starts the in-process JSON tool service (idempotent) and publishes its connection-*.json discovery
        /// file. It is torn down with the window. Publishing the connection file means that when the test is
        /// paused (e.g. a PauseTest / PauseForScreenShot showing the PauseAndContinueForm), an external MCP
        /// client can discover and drive this in-process Skyline -- the MCP already accepts the hosting
        /// TestRunner.exe process -- which is handy for working out how to drive a step.
        /// </summary>
        protected void StartToolService()
        {
            RunUI(() =>
            {
                // Pre-authorize MCP screen capture (in this process only -- not persisted) so an external
                // client driving a paused test is not blocked by the one-time permission handshake, e.g. when
                // grabbing a form image while working out how to drive a step.
                Settings.Default.AllowMcpScreenCapture = true;
                Program.StartToolService();
                Program.MainJsonToolServer.WriteConnectionInfo();
            });
        }

        /// <summary>
        /// Waits until a form of the given WinForms type appears among the connector's open forms
        /// (<see cref="IJsonToolService.GetOpenForms"/> -- the connector's own discovery method), then resolves
        /// and returns it as an <see cref="IFormElement"/> the test can drive (ClickButton, SetValue, Accept,
        /// Close, CaptureImage, ...).
        /// </summary>
        protected IFormElement WaitForConnectorForm<TForm>() where TForm : Form =>
            WaitForConnectorForm(typeof(TForm).Name);

        /// <summary>Waits for a form by its connector type name (see <see cref="WaitForConnectorForm{TForm}"/>).</summary>
        protected IFormElement WaitForConnectorForm(string typeName) =>
            ResolveWhenOpen(form => form.Type == typeName);

        /// <summary>
        /// Waits for the native common file dialog (Open / Save As) to appear -- it is enumerated by
        /// GetOpenForms with IsNative=true and type "FileDialog" -- and returns it as an IFormElement.
        /// </summary>
        protected IFormElement WaitForNativeFileDialog() =>
            ResolveWhenOpen(form => form.IsNative && form.Type == @"FileDialog");

        private IFormElement ResolveWhenOpen(Func<FormInfo, bool> predicate)
        {
            string id = null;
            WaitForCondition(() => null != (id = Connector.GetOpenForms().FirstOrDefault(predicate)?.Id));
            return JsonUiService.ResolveForm(id);
        }

        /// <summary>
        /// Waits until the form shows a control of the given type -- e.g. a wizard page's UserControl that swaps
        /// in after a transition (clicking "Next" can advance the page asynchronously). A screenshot taken right
        /// after then captures the settled page rather than a mid-transition frame. Uses the connector's
        /// GetControls, which lists only the controls currently shown (a not-yet-displayed page is not listed).
        /// </summary>
        protected void WaitForControl(IFormElement form, string controlType)
        {
            WaitForCondition(() =>
            {
                try
                {
                    return form.GetControls().Any(control => Equals(control.Path.Type, controlType));
                }
                catch (InvalidOperationException)
                {
                    // While the page is transitioning, the form can be briefly blocked by a transient dialog
                    // (e.g. the long-wait progress dialog shown while a spectral library loads), which makes
                    // GetControls throw. Treat that as "not ready yet" and keep polling until it clears.
                    return false;
                }
            });
        }

        /// <summary>
        /// Waits until the form's first control of the given type is enabled. A dialog that recomputes
        /// something on a background thread (e.g. AssociateProteinsDlg re-running its parsimony analysis when an
        /// option changes) disables its OK button while it works and re-enables it when the result is ready, so
        /// a test must wait for that before accepting.
        /// </summary>
        protected void WaitForControlEnabled(IFormElement form, string controlType)
        {
            WaitForCondition(() =>
            {
                try
                {
                    return form.GetControls().FirstOrDefault(control => Equals(control.Path.Type, controlType))?.Enabled == true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// Performs a connector action that is NOT expected to open a modal dialog, then waits for the posted
        /// (fire-and-forget) action to finish -- i.e. until <see cref="IJsonToolService.UnfinishedActionCount"/>
        /// returns to the value it had before. The count is incremented synchronously as the action is posted
        /// and decremented when its delegate returns, so this reliably waits out the click / value-set. For an
        /// action that DOES open a dialog (which stays counted until the dialog closes), wait for the dialog
        /// with <see cref="WaitForConnectorForm{TForm}"/> instead.
        /// </summary>
        protected void WaitForAction(Action action)
        {
            int before = Connector.UnfinishedActionCount();
            action();
            WaitForCondition(() => Connector.UnfinishedActionCount() == before);
        }

        /// <summary>
        /// Captures the given form's image through the connector (<see cref="IFormElement.CaptureImage"/>) and
        /// saves it as the next numbered tutorial screenshot. Unlike the screen-grab PauseForScreenShot, the
        /// image comes from the connector, the way an MCP client would request it.
        /// </summary>
        protected void PauseForScreenShot(IFormElement form, string description = null)
        {
            SaveConnectorScreenShot(form.CaptureImage);
        }

        /// <summary>
        /// The localized caption a control displays, read from its component's resources -- the same .resx the
        /// form or user control is built from, for the current UI language -- normalized the same way the
        /// connector normalizes a control's label (<see cref="UiElement.NormalizeLabel"/>: mnemonic '&amp;' and
        /// trailing punctuation removed). This lets a test match a control (e.g. a wizard's "Next &gt;" button)
        /// by the text the user actually sees, so it works in every language instead of hard-coding the English
        /// caption -- and the normalization makes it match the label the connector reports exactly.
        /// </summary>
        /// <typeparam name="T">The Form or UserControl type whose resources declare the control.</typeparam>
        /// <param name="controlName">The control's field name, e.g. "btnNext"; its "&lt;name&gt;.Text" entry is returned.</param>
        protected static string GetLocalizedText<T>(string controlName) where T : ContainerControl
        {
            return UiElement.NormalizeLabel(new ComponentResourceManager(typeof(T)).GetString(controlName + @".Text"));
        }

        /// <summary>
        /// Builds a localized, normalized menu path (e.g. "File &gt; Import &gt; Document") for
        /// <see cref="IJsonToolService.InvokeMenuItem"/> from the menu items' field names. Every segment is read
        /// from type <typeparamref name="T"/>'s resources -- the class that declares the menu items: a menu
        /// class such as ViewMenu/EditMenu/RefineMenu, or SkylineWindow for the File and Settings menus. The
        /// connector matches each segment by its visible caption, so this works in every UI language.
        /// </summary>
        protected static string MenuPath<T>(params string[] controlNames) where T : ContainerControl
        {
            return string.Join(@" > ", controlNames.Select(GetLocalizedText<T>));
        }
    }
}
