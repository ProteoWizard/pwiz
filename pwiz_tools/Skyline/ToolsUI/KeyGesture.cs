/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the keyboard on ONE control, whether or not it has the focus. Two separate things, because they
    /// are two different intents and mixing them would mean inventing an escape syntax for text:
    ///
    /// <para><see cref="SendText"/> TYPES -- it delivers each character to the control's own window as the
    /// WM_CHAR the message pump would, which is what inserts text and what raises Skyline's auto-completion
    /// popup. The text is literal throughout: no key names, no escaping, no reserved characters.</para>
    ///
    /// <para><see cref="SendKeyStroke"/> PRESSES ONE KEY, named with its modifiers ("Ctrl+V", "Down"). It
    /// raises the control's KeyDown with the composed <see cref="Keys"/> value, which is where a WinForms
    /// handler reads the keystroke from (Skyline's own paste handlers test <c>e.KeyData</c> exactly this way).
    /// Composing the value is what lets a modifier be expressed at all: a delivered key message carries only
    /// the virtual key, and WinForms would fill the modifiers in from the GLOBAL keyboard -- so "Ctrl+V" sent
    /// as a message arrives as a bare "V" unless the real keyboard state is doctored, which this deliberately
    /// does not do.</para>
    ///
    /// <para>A keystroke is atomic -- there is no way to press a key and leave it down. Nothing here can strand
    /// a key or a modifier in the down state.</para>
    ///
    /// <para>KNOWN LIMIT of the KeyDown route: it raises the event, it does not run the control's default
    /// window procedure. A key whose effect comes from that default handling rather than from a handler --
    /// Backspace editing a text box, an arrow moving a plain list's selection -- will not take effect through
    /// <see cref="SendKeyStroke"/>. Handler-driven keys (Skyline's auto-completion popup, its grid paste) do.
    /// </para>
    /// </summary>
    internal static class KeyGesture
    {
        // Spellings a caller is likely to use for keys whose Keys name differs. Everything else is matched
        // against the Keys enum itself, so "V", "Down", "F2", "Delete", "Space" all just work.
        private static readonly Dictionary<string, Keys> KEY_ALIASES =
            new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
            {
                { @"CTRL", Keys.Control }, { @"CONTROL", Keys.Control },
                { @"ALT", Keys.Alt }, { @"SHIFT", Keys.Shift },
                { @"ENTER", Keys.Return }, { @"ESC", Keys.Escape },
                { @"DEL", Keys.Delete }, { @"INS", Keys.Insert },
                { @"BACKSPACE", Keys.Back }, { @"BS", Keys.Back },
                { @"PGUP", Keys.PageUp }, { @"PGDN", Keys.PageDown }
            };

        private static readonly Keys[] MODIFIER_KEYS = { Keys.Control, Keys.Alt, Keys.Shift };

        /// <summary>Types <paramref name="text"/> into <paramref name="control"/>, character by character. The
        /// text is taken literally. Must be called on the control's UI thread.</summary>
        public static void SendText(Control control, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var handle = control.Handle;
            foreach (char c in text)
                User32.SendMessage(handle, User32.WinMessageType.WM_CHAR, (IntPtr) c, IntPtr.Zero);
        }

        /// <summary>Presses one key on <paramref name="control"/>. <paramref name="keyStroke"/> names the key
        /// with any modifiers, '+'-separated and in any order -- "Ctrl+V", "Down", "Enter", "Ctrl+Shift+Home",
        /// "Alt+F4". Must be called on the control's UI thread.</summary>
        public static void SendKeyStroke(Control control, string keyStroke)
        {
            ControlElement.RaiseProtectedHandler(control, @"OnKeyDown", new KeyEventArgs(Parse(keyStroke)));
        }

        /// <summary>The <see cref="Keys"/> value <paramref name="keyStroke"/> names -- the key OR-ed with its
        /// modifiers. Throws an LLM-facing error naming the offending segment when it cannot be read.</summary>
        public static Keys Parse(string keyStroke)
        {
            if (string.IsNullOrWhiteSpace(keyStroke))
                throw new ArgumentException(new LlmInstruction(
                    @"No key given. Name a key, with any modifiers, e.g. 'Down', 'Enter' or 'Ctrl+V'."));

            var keyData = Keys.None;
            bool hasKey = false;
            foreach (var segment in keyStroke.Split('+').Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                if (!KEY_ALIASES.TryGetValue(segment, out var key) &&
                    !Enum.TryParse(segment, true, out key))
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Unknown key '{0}' in '{1}'. Use a key name (A-Z, 0-9, Enter, Down, Up, Left, Right, Tab, Esc, Backspace, Delete, Home, End, PgUp, PgDn, F1-F12, Space) with optional Ctrl+, Shift+ and Alt+ modifiers.",
                        segment, keyStroke));
                }
                if (MODIFIER_KEYS.Contains(key))
                {
                    keyData |= key;
                }
                else if (hasKey)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"'{0}' names more than one key. A key stroke is a single key with modifiers, e.g. 'Ctrl+V'.",
                        keyStroke));
                }
                else
                {
                    keyData |= key;
                    hasKey = true;
                }
            }
            if (!hasKey)
                throw new ArgumentException(LlmInstruction.Format(
                    @"'{0}' names only modifiers. Add the key they apply to, e.g. 'Ctrl+V'.", keyStroke));
            return keyData;
        }
    }
}
