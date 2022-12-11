/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace pwiz.SkylineTestUtil
{
    public static class TypeInspector
    {
        /// <summary>
        /// Returns the size of an object in memory.
        /// If "type" is a struct, then returns the size of the "boxed" object.
        /// </summary>
        public static int GetObjectSize(Type type)
        {
            return Marshal.ReadInt32(type.TypeHandle.Value, 4);
        }

        /// <summary>
        /// Returns the size that a field of the specified type would take up
        /// in an object.
        /// </summary>
        public static double GetFieldSize(Type type)
        {
            var subclassType = typeof(Subclass2<>).MakeGenericType(type);
            return (GetObjectSize(subclassType) - GetObjectSize(typeof(Subclass1))) / 16.0;
        }

        public static void DumpTypeAndFields(Type type, TextWriter writer)
        {
            writer.WriteLine(TabSeparate("Type: " + EscapeCode(type.FullName), "Object size: " + GetObjectSize(type)));
            DumpFields(type, writer);
            while (true)
            {
                type = type.BaseType;
                if (type == null || type.IsInterface)
                {
                    return;
                }
                writer.WriteLine(TabSeparate("BaseType: " + EscapeCode(type.FullName), "Object size: " + GetObjectSize(type)));
                DumpFields(type, writer);
            }
        }

        public static void DumpFields(Type type, TextWriter writer)
        {
            bool first = true;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (first)
                {
                    writer.WriteLine(TabSeparate("Field Name", "Type", "Field Size"));
                    first = false;
                }
                writer.WriteLine(TabSeparate(EscapeCode(field.Name), EscapeCode(field.FieldType.FullName), GetFieldSize(field.FieldType)));
            }
        }

        private static string TabSeparate(params object[] values)
        {
            return string.Join("\t", values);
        }

        private static string EscapeCode(string code)
        {
            return "`" + code + "`";
        }
#pragma warning disable CS0649 // Disable "Field is never assigned" warning
        /// <summary>
        /// Class which is used to help determine the size of fields
        /// </summary>
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        internal class Subclass1 : object
        {
            public int w;
            public int x;
            public int y;
            public int z;
        }

        /// <summary>
        /// Generic class which is used to help determine the size of fields
        /// </summary>
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        internal class Subclass2<T> : Subclass1
        {
            public T _myValue1;
            public T _myValue2;
            public T _myValue3;
            public T _myValue4;
            public T _myValue5;
            public T _myValue6;
            public T _myValue7;
            public T _myValue8;
            public T _myValue9;
            public T _myValue10;
            public T _myValue11;
            public T _myValue12;
            public T _myValue13;
            public T _myValue14;
            public T _myValue15;
            public T _myValue16;
        }
    }
}
