/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Common.Properties;

namespace pwiz.Common.SystemUtil
{
    public static class StreamEx
    {
        public static void TransferBytes(this Stream inStream, Stream outStream, long bytesToTransfer)
        {
            int bufferSize = (int)Math.Min(bytesToTransfer, 0x40000); // 256K;
            inStream.TransferBytes(outStream, bytesToTransfer, new byte[bufferSize]);
        }

        public static void TransferBytes(this Stream inStream, Stream outStream, long bytesToTransfer, byte[] buffer)
        {
            long bytesTransferred = 0;
            long bytesRemaining = bytesToTransfer;
            int len;
            while (bytesToTransfer > 0 && (len = inStream.Read(buffer, 0, (int)Math.Min(bytesRemaining, buffer.Length))) != 0)
            {
                outStream.Write(buffer, 0, len);
                bytesRemaining -= len;
                bytesTransferred += len;
            }

            if (bytesTransferred != bytesToTransfer)
            {
                throw new InvalidDataException(string.Format(
                    @"Tried to transfer {0} bytes, but actual byte count transferred was {1}", bytesToTransfer,
                    bytesTransferred));
            }
        }

        public static void ReadOrThrow(this Stream stream, [In, Out] byte[] buffer, int offset, int count)
        {
            if (count < stream.Read(buffer, offset, count))
                throw new IOException(string.Format(Resources.StreamEx_ReadOrThrow_Failed_reading__0__bytes__Source_may_be_corrupted_, count));
        }

        public static byte[] ReadBytes(this Stream inStream, int byteCount)
        {
            var buffer = new byte[byteCount];
            int bytesRead = inStream.Read(buffer, 0, buffer.Length);
            if (bytesRead != buffer.Length)
            {
                throw new InvalidDataException(string.Format(
                    @"Tried to read {0} bytes, but actual byte count read was {1}", buffer.Length, bytesRead));
            }

            return buffer;
        }
    }
}