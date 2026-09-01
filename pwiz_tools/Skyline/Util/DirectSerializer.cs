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
                    // Save the caller's logical position, then Flush() to make FileStream
                    // discard its read-ahead buffer and align its cursor + the OS handle
                    // to that position. On net472 the FileStream.Read strategy did this
                    // implicitly on every operation; on net8 the buffer is preserved
                    // across the P/Invoke read, so the SafeFileHandle read pulls bytes
                    // from a position AHEAD of the logical position. After the P/Invoke,
                    // manually restore FileStream's logical position past the bytes we
                    // read - the OS cursor is already there, but FileStream's cached
                    // _position is stale.
                    long logicalPos = stream.Position;
                    stream.Flush();
                    stream.Position = logicalPos;
                    var result = _readFunc(stream.SafeFileHandle, count);
                    stream.Position = logicalPos + System.Runtime.InteropServices.Marshal.SizeOf<TItem>() * (long)count;
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
                    // Same rationale as ReadArray - Flush to align the OS cursor with
                    // FileStream's logical position before the P/Invoke write, then
                    // manually advance FileStream's cached position past the bytes we
                    // wrote so subsequent FileStream writes go to the right offset.
                    long logicalPos = stream.Position;
                    stream.Flush();
                    stream.Position = logicalPos;
                    _writeAction(stream.SafeFileHandle, items);
                    stream.Position = logicalPos + System.Runtime.InteropServices.Marshal.SizeOf<TItem>() * (long)items.Length;
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
