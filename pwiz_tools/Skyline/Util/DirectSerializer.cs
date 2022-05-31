/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Runtime.InteropServices;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Provides the functionality for <see cref="StructSerializer{TItem}"/> to
    /// be able to use <see cref="FastRead"/> and <see cref="FastWrite"/> for 
    /// a particular Type of Struct.
    /// </summary>
    public static class DirectSerializer
    {
        public static IDirectSerializer<TItem> Create<TItem>(
            Func<SafeHandle, int, TItem[]> readFunc,
            Action<SafeHandle, TItem[]> writeAction)
        {
            return new DelegateImpl<TItem>(readFunc, writeAction);
        }

        private class DelegateImpl<TItem> : IDirectSerializer<TItem>
        {
            private readonly Func<SafeHandle, int, TItem[]> _readFunc;
            private readonly Action<SafeHandle, TItem[]> _writeAction;
            public DelegateImpl(Func<SafeHandle, int, TItem[]> readFunc, Action<SafeHandle, TItem[]> writeAction)
            {
                _readFunc = readFunc;
                _writeAction = writeAction;
            }

            public TItem[] ReadArray(FileStream stream, int count)
            {
                try
                {
                    var result = _readFunc(stream.SafeFileHandle, count);
                    stream.Seek(0, SeekOrigin.Current);
                    return result;
                }
                catch (BulkReadException)
                {
                    return null;
                }
            }

            public bool WriteArray(FileStream stream, TItem[] items)
            {
                if (_writeAction == null)
                {
                    return false;
                }
                try
                {
                    _writeAction(stream.SafeFileHandle, items);
                    // Tell the stream to ask for its current position from the underlying file handle.
                    stream.Seek(0, SeekOrigin.Current);
                    return true;
                }
                catch (BulkReadException)
                {
                    return false;
                }
            }
        }
    }
}
