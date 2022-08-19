/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    public static class ClipboardEx
    {
        public const string SKYLINE_FORMAT = "Skyline Format";

        private static bool _useSystemClipboard = true;
        private static DataObject _dataObject;

        // Set this true to check that the internal clipboard returns the
        // same values as the system clipboard.
// ReSharper disable ConvertToConstant.Local
// ReSharper disable RedundantDefaultFieldInitializer
        private static readonly bool CHECK_VALUES = false;
// ReSharper restore RedundantDefaultFieldInitializer
// ReSharper restore ConvertToConstant.Local

        public static void UseInternalClipboard(bool useInternal = true)
        {
            _useSystemClipboard = !useInternal;
        }

        /// <summary>
        /// Returns a list of the available formats on the clipboard now.
        /// </summary>
        public static string[] GetClipboardFormats()
        {
            var dataObject = _useSystemClipboard ? Clipboard.GetDataObject() : _dataObject;
            if (dataObject == null)
            {
                return new string[0];
            }

            lock (dataObject)
            {
                return dataObject.GetFormats();
            }
        }

        public static void SetDataObject(DataObject data, bool copy)
        {
            if (_useSystemClipboard)
            {
                Clipboard.SetDataObject(data, copy);
            }
            else
            {
                _dataObject = data;
                if (CHECK_VALUES)
                {
                    Clipboard.SetDataObject(data, copy);
                }
            }
        }

        public static object GetData(string format)
        {
            if (_useSystemClipboard)
            {
                return Clipboard.GetData(format);
            }
            var dataObject = _dataObject;
            lock (dataObject)
            {
                object data = dataObject.GetData(format);
                if (CHECK_VALUES)
                {
                    object expected = Clipboard.GetData(format);
                    if (((data == null || expected == null) && data != expected) ||
                        (data != null && data.ToString() != Clipboard.GetData(format).ToString()))
                    {
                        throw new ApplicationException(Resources.ClipboardEx_GetData_ClipboardEx_implementation_problem);
                    }
                }
                return data;
            }
        }

        public static void Clear()
        {
            if (_useSystemClipboard)
            {
                Clipboard.Clear();
            }
            else
            {
                _dataObject = new DataObject();
                if (CHECK_VALUES)
                {
                    Clipboard.Clear();
                }
            }
        }

        public static void Release()
        {
            Clear();
            _dataObject = null;
        }

        public static void SetText(string text)
        {
            if (_useSystemClipboard)
            {
                Clipboard.SetText(text);
            }
            else
            {
                var dataObject = _dataObject;
                lock (dataObject)
                {
                    dataObject.SetText(text, TextDataFormat.Text);
                    if (CHECK_VALUES)
                    {
                        Clipboard.SetText(text);
                    }
                }
            }
        }

        public static void SetText(string text, TextDataFormat format)
        {
            if (_useSystemClipboard)
            {
                Clipboard.SetText(text, format);
            }
            else
            {
                var dataObject = _dataObject;
                lock (dataObject)
                {
                    if (format != TextDataFormat.Text)
                    {
                        throw new ApplicationException(Resources
                            .ClipboardEx_GetData_ClipboardEx_implementation_problem);
                    }

                    dataObject.SetData(text, DataFormats.Text);
                    if (CHECK_VALUES)
                    {
                        Clipboard.SetText(text, format);
                    }
                }
            }
        }

        public static string GetText()
        {
            if (_useSystemClipboard)
            {
                return Clipboard.GetText();
            }

            var dataObject = _dataObject;
            lock (dataObject) 
            {
                string text = (string)dataObject.GetData(DataFormats.Text);
                if (CHECK_VALUES && text != Clipboard.GetText())
                {
                    throw new ApplicationException(Resources.ClipboardEx_GetData_ClipboardEx_implementation_problem);
                }
                return text;
            }
        }

        public static string GetText(TextDataFormat format)
        {
            if (_useSystemClipboard)
            {
                return Clipboard.GetText(format);
            }

            var dataObject = _dataObject;
            lock (dataObject)
            {
                string text = string.Empty;
                if (format == TextDataFormat.Text)
                {
                    text = (string)dataObject.GetData(DataFormats.Text);
                }
                if (CHECK_VALUES && text != Clipboard.GetText(format))
                {
                    throw new ApplicationException(Resources.ClipboardEx_GetData_ClipboardEx_implementation_problem);
                }
                return text;
            }
        }
    }
}
