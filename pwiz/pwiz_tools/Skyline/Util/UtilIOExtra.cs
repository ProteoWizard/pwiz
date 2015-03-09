/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Text;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    ///  Adler 32 check sum calculation
    /// <para>
    ///  Written by Youry Jukov (yjukov@hotmail.com)
    ///      http://www.codeproject.com/KB/recipes/Adler-32_checksum.aspx
    ///  Modified by Brendan MacLean (12/30/2008)
    ///  - Improved file handling
    ///  - Applied ReSharper directed clean-up
    ///  </para><para>
    ///  From en.wikipedia.org:
    ///  </para><para>
    ///  Adler-32 is a checksum algorithm which was invented by Mark Adler.
    ///  It is almost as reliable as a 32-bit cyclic redundancy check for 
    ///  protecting against accidental modification of data, such as distortions 
    ///  occurring during a transmission.
    ///  An Adler-32 checksum is obtained by calculating two 16-bit checksums A and B and 
    ///  concatenating their bits into a 32-bit integer. A is the sum of all bytes in the 
    ///  string, B is the sum of the individual values of A from each step.
    ///  At the beginning of an Adler-32 run, A is initialized to 1, B to 0.
    ///  The sums are done modulo 65521 (the largest prime number smaller than 216). 
    ///  The bytes are stored in network order (big endian), B occupying 
    ///  the two most significant bytes.
    ///  The function may be expressed as
    ///  </para><code>
    ///  A = 1 + D1 + D2 + ... + DN (mod 65521)
    ///  B = (1 + D1) + (1 + D1 + D2) + ... + (1 + D1 + D2 + ... + DN) (mod 65521)
    ///    = N×D1 + (N-1)×D2 + (N-2)×D3 + ... + DN + N (mod 65521)
    ///  
    ///  Adler-32(D) = B * 65536 + A
    ///  </code><para>
    ///  where D is the string of bytes for which the checksum is to be calculated,
    ///  and N is the length of D.
    /// </para><para>
    /// Jonathan Stone discovered in 2001 that Adler-32 has a weakness for very short
    /// messages. He wrote "Briefly, the problem is that, for very short packets, Adler32
    /// is guaranteed to give poor coverage of the available bits. Don't take my word for
    /// it, ask Mark Adler. :-)" The problem is that sum A does not wrap for short
    /// messages. The maximum value of A for a 128-byte message is 32640, which is below
    /// the value 65521 used by the modulo operation. An extended explanation can be found
    /// in RFC 3309, which mandates the use of CRC32 instead of Adler-32 for SCTP, the
    /// Stream Control Transmission Protocol.</para>
    /// </summary>
    public class AdlerChecksum
    {
        #region parameters

        /// <summary>
        /// ADLER_BASE is Adler-32 checksum algorithm parameter.
        /// </summary>
        public const uint ADLER_BASE = 0xFFF1;
        /// <summary>
        /// ADLER_START is Adler-32 checksum algorithm parameter.
        /// </summary>
        public const uint ADLER_START = 0x0001;
        /// <summary>
        /// ADLER_BUFF is Adler-32 checksum algorithm parameter.
        /// </summary>
        public const uint ADLER_BUFF = 0x0400;

        #endregion

        public static uint MakeForBuff(byte[] bytesBuff)
        {
            var inst = new AdlerChecksum();
            if (!inst.TryMakeForBuff(bytesBuff))
                throw new InvalidOperationException(Resources.AdlerChecksum_MakeForBuff_Invalid_byte_buffer_for_checksum);
            return inst.ChecksumValue;
        }

        public static uint MakeForString(string s)
        {
            var inst = new AdlerChecksum();
            if (!inst.TryMakeForString(s))
                throw new InvalidOperationException(string.Format(Resources.AdlerChecksum_MakeForString_Invalid_string___0___for_checksum, s ?? "(null)")); // Not L10N
            return inst.ChecksumValue;
        }

        public static uint MakeForFile(string sPath)
        {
            var inst = new AdlerChecksum();
            if (!inst.TryMakeForFile(sPath))
                throw new IOException(
                    string.Format(
                        Resources.AdlerChecksum_MakeForFile_Failure_attempting_to_calculate_a_checksum_for_the_file__0__,
                        sPath));
            return inst.ChecksumValue;
        }

        /// <value>
        /// ChecksumValue is property which enables the user
        /// to get Adler-32 checksum value for the last calculation 
        /// </value>
        public uint ChecksumValue { get; private set; }

        /// <summary>
        /// Calculate Adler-32 checksum for buffer
        /// </summary>
        /// <param name="bytesBuff">Bites array for checksum calculation</param>
        /// <param name="unAdlerCheckSum">Checksum start value (default=1)</param>
        /// <returns>Returns true if the checksum values is successflly calculated</returns>
        public bool TryMakeForBuff(byte[] bytesBuff, uint unAdlerCheckSum)
        {
            if (Equals(bytesBuff, null))
            {
                ChecksumValue = 0;
                return false;
            }
            int nSize = bytesBuff.Length;
            if (nSize == 0)
            {
                ChecksumValue = 0;
                return false;
            }
            uint unSum1 = unAdlerCheckSum & 0xFFFF;
            uint unSum2 = (unAdlerCheckSum >> 16) & 0xFFFF;
            for (int i = 0; i < nSize; i++)
            {
                unSum1 = (unSum1 + bytesBuff[i]) % ADLER_BASE;
                unSum2 = (unSum1 + unSum2) % ADLER_BASE;
            }
            ChecksumValue = (unSum2 << 16) + unSum1;
            return true;
        }

        /// <summary>
        /// Calculate Adler-32 checksum for buffer.
        /// </summary>
        /// <param name="bytesBuff">Byte array for checksum calculation</param>
        /// <returns>Returns true if the checksum values is successflly calculated</returns>
        public bool TryMakeForBuff(byte[] bytesBuff)
        {
            return TryMakeForBuff(bytesBuff, ADLER_START);
        }

        /// <summary>
        /// Calculate Adler-32 checksum for string.
        /// </summary>
        /// <param name="s">String to convert to bytes for checksum calculation</param>
        /// <returns>Returns true if the checksum values is successflly calculated</returns>
        public bool TryMakeForString(string s)
        {
            return !string.IsNullOrEmpty(s) && TryMakeForBuff(Encoding.UTF8.GetBytes(s));
        }

        /// <summary>
        /// Calculate Adler-32 checksum for file
        /// </summary>
        /// <param name="sPath">Path to file for checksum calculation</param>
        /// <returns>Returns true if the checksum values is successflly calculated</returns>
        public bool TryMakeForFile(string sPath)
        {
            if (!File.Exists(sPath))
            {
                ChecksumValue = 0;
                return false;
            }

            FileStream fs = null;
            try
            {
                fs = new FileStream(sPath, FileMode.Open, FileAccess.Read);
                if (fs.Length == 0)
                {
                    ChecksumValue = 0;
                    return false;
                }

                ChecksumValue = ADLER_START;
                byte[] bytesBuff = new byte[ADLER_BUFF];
                for (uint i = 0; i < fs.Length; i++)
                {
                    uint index = i % ADLER_BUFF;
                    bytesBuff[index] = (byte)fs.ReadByte();
                    if ((index == ADLER_BUFF - 1) || (i == fs.Length - 1))
                    {
                        if (!TryMakeForBuff(bytesBuff, ChecksumValue))
                        {
                            ChecksumValue = 0;
                            return false;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                ChecksumValue = 0;
                return false;
            }
            catch (IOException)
            {
                ChecksumValue = 0;
                return false;
            }
            finally
            {
                if (fs != null)
                {
                    try { fs.Close(); }
                    catch (IOException) { }
                }
            }
            return true;
        }

        #region object overrides

        public bool Equals(AdlerChecksum obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.ChecksumValue == ChecksumValue;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(AdlerChecksum)) return false;
            return Equals((AdlerChecksum)obj);
        }

        public override int GetHashCode()
        {
            return ChecksumValue.GetHashCode();
        }

        public static bool operator ==(AdlerChecksum left, AdlerChecksum right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AdlerChecksum left, AdlerChecksum right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// ToString is a method for current AdlerChecksum object
        /// representation in textual form.
        /// </summary>
        /// <returns>Returns current checksum or
        /// or "Unknown" if checksum value is unavailable 
        /// </returns>
        public override string ToString()
        {
            if (ChecksumValue != 0)
                return ChecksumValue.ToString(CultureInfo.InvariantCulture);
            return Resources.AdlerChecksum_ToString_Unknown;
        }

        #endregion
    }
}
