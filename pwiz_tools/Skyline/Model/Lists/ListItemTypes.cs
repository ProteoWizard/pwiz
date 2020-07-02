/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace pwiz.Skyline.Model.Lists
{
    /// <summary>
    /// Handles defining a new Class for every list name.
    /// The <see cref="pwiz.Common.DataBinding.DataSchema"/> uses the Type to figure out what properties an object has.
    /// The ListItemTypes object defines a new Type whenever a new list name is needed,
    /// and handles the mapping from these newly defined type to the list name.
    /// </summary>
    public class ListItemTypes
    {
        public static readonly ListItemTypes INSTANCE = new ListItemTypes();

        private const string NAMESPACE = "pwiz.Skyline.Model.Lists.DynamicTypes";
        private readonly ModuleBuilder _moduleBuilder;
        private readonly IDictionary<string, Type> _listTypes = new Dictionary<string, Type>();
        private readonly IDictionary<string, string> _listNames = new Dictionary<string, string>();

        private ListItemTypes()
        {
            var assemblyName = new AssemblyName(@"ListItemTypes" + Guid.NewGuid());
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            _moduleBuilder = assemblyBuilder.DefineDynamicModule(@"ListItemTypes");
        }

        public Type GetListItemType(string listName)
        {
            lock (this)
            {
                Type type;
                if (_listTypes.TryGetValue(listName, out type))
                {
                    return type;
                }
                var fullyQualifiedTypeName = NAMESPACE + '.' + MakeValidIdentifier(listName, _listTypes.Count);
                type = DefineListItemType(fullyQualifiedTypeName);
                _listTypes.Add(listName, type);
                // ReSharper disable AssignNullToNotNullAttribute
                _listNames.Add(type.FullName, listName);
                // ReSharper restore AssignNullToNotNullAttribute
                return type;
            }
        }

        public string GetListName(Type type)
        {
            lock (this)
            {
                string listName;
                // ReSharper disable AssignNullToNotNullAttribute
                _listNames.TryGetValue(type.FullName, out listName);
                // ReSharper restore AssignNullToNotNullAttribute
                return listName;
            }
        }

        public string GetListName<T>() where T : ListItem
        {
            return GetListName(typeof(T));
        }

        private Type DefineListItemType(string fullyQualifiedTypeName)
        {
            var typeBuilder = _moduleBuilder.DefineType(fullyQualifiedTypeName);
            typeBuilder.SetParent(typeof(ListItem));
            return typeBuilder.CreateType();
        }

        private static string MakeValidIdentifier(string listName, int uniquefier)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var ch in listName)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    stringBuilder.Append(ch);
                }

                if (stringBuilder.Length > 100)
                {
                    break;
                }
            }
            stringBuilder.Append('_');
            stringBuilder.Append(uniquefier);
            return stringBuilder.ToString();
        }
    }
}
