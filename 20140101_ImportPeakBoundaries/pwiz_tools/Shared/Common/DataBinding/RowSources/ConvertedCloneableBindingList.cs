/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

using System.ComponentModel;

namespace pwiz.Common.DataBinding.RowSources
{
    public abstract class ConvertedCloneableBindingList<TKey, TSource, TTarget>
        : ConvertedCloneableList<TKey, TSource, TTarget>, IListChanged
    {
        protected ConvertedCloneableBindingList(IListChanged list) 
            : base((ICloneableList<TKey, TSource>) list)
        {
        }
        // ReSharper disable SuspiciousTypeConversion.Global
        protected ConvertedCloneableBindingList(IBindingList list)
            : base((ICloneableList<TKey, TSource>)list)
        {
        }
        // ReSharper restore SuspiciousTypeConversion.Global
        public event ListChangedEventHandler ListChanged
        {
            add
            {
                var listChanged = SourceList as IListChanged;
                if (listChanged != null)
                {
                    listChanged.ListChanged += WrapListChangedEventHandler(value);
                }
                else
                {
                    // ReSharper disable SuspiciousTypeConversion.Global
                    var bindingList = SourceList as IBindingList;
                    // ReSharper restore SuspiciousTypeConversion.Global
                    if (bindingList != null)
                    {
                        bindingList.ListChanged += WrapListChangedEventHandler(value);
                    }
                }
            }
            remove 
            { 
                var listChanged = SourceList as IListChanged;
                if (listChanged != null)
                {
                    listChanged.ListChanged -= WrapListChangedEventHandler(value);
                }
                else
                {
// ReSharper disable SuspiciousTypeConversion.Global
                    var bindingList = SourceList as IBindingList;
// ReSharper restore SuspiciousTypeConversion.Global
                    if (bindingList != null)
                    {
                        bindingList.ListChanged += WrapListChangedEventHandler(value);
                    }
                }
            }
        }

        private ListChangedEventHandler WrapListChangedEventHandler(ListChangedEventHandler eventHandler)
        {
            return (sender, args) => eventHandler(this, args);
        }
    }
}