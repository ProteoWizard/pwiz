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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace pwiz.Common.Collections
{
    public interface IItemSerializer<TItem>
    {
        TItem[] ReadArray(Stream stream, int count);
        void WriteItems(Stream stream, IEnumerable<TItem> items);
    }

    /// <summary>
    /// Provides the functionality needed for "FastRead" for a particular
    /// struct.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public interface IDirectSerializer<TItem>
    {
        TItem[] ReadArray(FileStream fileStream, int count);
        bool WriteArray(FileStream fileStream, TItem[] items);
    }

    
    /// <summary>
    /// Reads and writes structs from a Stream using methods provided on the <see cref="Marshal"/> 
    /// class. In order to handle the fact that over time more fields may be added to the struct,
    /// the value "ItemSizeOnDisk" can be changed. If "PadFromStart" is false, then new fields
    /// need to get added to the end of the structure.
    /// </summary>
    public class StructSerializer<TItem> : IItemSerializer<TItem> where TItem : struct
    {
        public StructSerializer()
        {
            ItemSizeInMemory = Marshal.SizeOf<TItem>();
            ItemSizeOnDisk = ItemSizeInMemory;
        }

        public int ItemSizeInMemory { get; private set; }
        public int ItemSizeOnDisk { get; set; }
        public bool PadFromStart { get; set; }
        public IDirectSerializer<TItem> DirectSerializer { get; set; }

        public TItem[] ReadArray(Stream stream, int count)
        {
            TItem[] result = TryDirectRead(stream, count);
            if (result != null)
            {
                return result;
            }
            result = new TItem[count];
            var buffer = new byte[ItemSizeOnDisk];
            for (int i = 0; i < count; i++)
            {
                if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
                    throw new InvalidDataException();
                result[i] = FromByteArray(buffer);
            }
            return result;
        }

        public void WriteItems(Stream stream, IEnumerable<TItem> items)
        {
            var itemArray = items as TItem[];
            if (itemArray != null)
            {
                if (TryFastWrite(stream, itemArray))
                {
                    return;
                }
            }
            foreach (var item in items)
            {
                var buffer = ResizeByteArray(ToByteArray(item), ItemSizeOnDisk);
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        public byte[] ResizeByteArray(byte[] byteArray, int newSize)
        {
            if (byteArray.Length == newSize)
            {
                return byteArray;
            }
            var newByteArray = new byte[newSize];
            if (PadFromStart)
            {
                Array.Copy(byteArray, Math.Max(0, byteArray.Length - newSize),
                    newByteArray, Math.Max(0, newSize - byteArray.Length),
                    Math.Min(byteArray.Length, newSize));
            }
            else
            {
                Array.Copy(byteArray, newByteArray, Math.Min(byteArray.Length, newSize));
            }
            return newByteArray;
        }

        public byte[] ToByteArray(TItem item)
        {
            byte[] bytes = new byte[ItemSizeInMemory];
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.StructureToPtr(item, ptr, true);
                Marshal.Copy(ptr, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public TItem FromByteArray(byte[] bytes)
        {
            bytes = ResizeByteArray(bytes, ItemSizeInMemory);
            IntPtr ptr = Marshal.AllocHGlobal(ItemSizeInMemory);
            try
            {
                Marshal.Copy(bytes, 0, ptr, ItemSizeInMemory);
                return Marshal.PtrToStructure<TItem>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        protected TItem[] TryDirectRead(Stream stream, int count)
        {
            if (null == DirectSerializer)
            {
                return null;
            }
            if (ItemSizeInMemory != ItemSizeOnDisk)
            {
                return null;
            }
            FileStream fileStream = stream as FileStream;
            if (fileStream == null)
            {
                return null;
            }
            return DirectSerializer.ReadArray(fileStream, count);
        }

        protected bool TryFastWrite(Stream stream, TItem[] items)
        {
            if (null == DirectSerializer)
            {
                return false;
            }
            if (ItemSizeInMemory != ItemSizeOnDisk)
            {
                return false;
            }
            FileStream fileStream = stream as FileStream;
            if (null == fileStream)
            {
                return false;
            }
            return DirectSerializer.WriteArray(fileStream, items);
        }
    }

    /// <summary>
    /// Provides an implementation of IItemSerializer which wraps another IItemSerializer, and 
    /// performs a mapping to a different objet.
    /// </summary>
    public static class ConvertedItemSerializer
    {
        public static IItemSerializer<TCurrentItem> Create<TCurrentItem, TLegacyItem>(
            IItemSerializer<TLegacyItem> legacyReader, 
            Func<TLegacyItem, TCurrentItem> fromLegacyConverter, 
            Func<TCurrentItem, TLegacyItem> toLegacyConverter)
        {
            return new Impl<TCurrentItem,TLegacyItem>(legacyReader, fromLegacyConverter, toLegacyConverter);
        }
        private class Impl<TCurrentItem, TLegacyItem> : IItemSerializer<TCurrentItem>
        {
            private readonly Func<TLegacyItem, TCurrentItem> _fromLegacyConverter;
            private readonly Func<TCurrentItem, TLegacyItem> _toLegacyConverter;
            public Impl(IItemSerializer<TLegacyItem> legacyReader, 
                Func<TLegacyItem, TCurrentItem> fromLegacyConverter, 
                Func<TCurrentItem, TLegacyItem> toLegacyConverter)
            {
                LegacyReader = legacyReader;
                _fromLegacyConverter = fromLegacyConverter;
                _toLegacyConverter = toLegacyConverter;
            }

            public IItemSerializer<TLegacyItem> LegacyReader { get; protected set; }
            public TCurrentItem[] ReadArray(Stream stream, int count)
            {
                var legacyItems = LegacyReader.ReadArray(stream, count);
                return legacyItems.Select(_fromLegacyConverter).ToArray();
            }

            public void WriteItems(Stream stream, IEnumerable<TCurrentItem> items)
            {
                LegacyReader.WriteItems(stream, items.Select(_toLegacyConverter));
            }
        }
    }
}
