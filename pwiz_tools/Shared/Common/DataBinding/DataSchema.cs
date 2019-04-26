/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Handles property inspection on types.
    /// Applications can override this class in order to add properties to types to
    /// include user-defined properties.
    /// </summary>
    public class DataSchema
    {
        public DataSchema() : this(DataSchemaLocalizer.INVARIANT)
        {
        }

        public DataSchema(DataSchemaLocalizer dataSchemaLocalizer) : this(new QueryLock(CancellationToken.None), dataSchemaLocalizer)
        {
        }

        public DataSchema(QueryLock queryLock, DataSchemaLocalizer dataSchemaLocalizer)
        {
            DataSchemaLocalizer = dataSchemaLocalizer;
            QueryLock = queryLock;
        }

        public QueryLock QueryLock { get; private set; }

        /// <summary>
        /// Returns the properties for the specified type.
        /// </summary>
        public virtual IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
        {
            if (null == type)
            {
                return new PropertyDescriptor[0];
            }
            var chainParent = GetChainedPropertyDescriptorParent(type);
            if (chainParent != null)
            {
                return GetPropertyDescriptors(chainParent.PropertyType)
                    .Select(pd => (PropertyDescriptor) new ChainedPropertyDescriptor(pd.Name, chainParent, pd));
            }
            if (IsScalar(type))
            {
                return new PropertyDescriptor[0];
            }
            return ListProperties(new HashSet<string>(), type);
        }

        public virtual string DefaultUiMode
        {
            get { return string.Empty; }
        }

        public virtual string NormalizeUiMode(string uiMode)
        {
            return uiMode;
        }

        protected virtual IEnumerable<PropertyDescriptor> ListProperties(HashSet<string> propertyNames, Type type)
        {
            if (IsScalar(type))
            {
                return new PropertyDescriptor[0];
            }
            var propertyDescriptors = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(type))
            {
                if (!propertyNames.Add(propertyDescriptor.Name))
                {
                    continue;
                }
                if (propertyDescriptor.IsBrowsable)
                {
                    propertyDescriptors.Add(propertyDescriptor);
                }
            }
            if (type.IsInterface)
            {
                foreach (var baseInterface in type.GetInterfaces())
                {
                    propertyDescriptors.AddRange(ListProperties(propertyNames, baseInterface));
                }
            }
            if (null != type.BaseType)
            {
                propertyDescriptors.AddRange(ListProperties(propertyNames, type.BaseType));
            }
            return propertyDescriptors;
        }

        /// <summary>
        /// Returns the property descriptor with the specified name.
        /// </summary>
        public virtual PropertyDescriptor GetPropertyDescriptor(Type type, string name)
        {
            return GetPropertyDescriptors(type).FirstOrDefault(pd => pd.Name == name);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public ICollectionInfo GetCollectionInfo(Type type)
        {
            return CollectionInfo.ForType(type);
        }
        /// <summary>
        /// Returns true if the property is one that can be displayed in a DataGridView.
        /// </summary>
        public virtual bool IsBrowsable(PropertyDescriptor propertyDescriptor)
        {
            if (!propertyDescriptor.IsBrowsable)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if the type has no properties.
        /// </summary>
        protected virtual bool IsScalar(Type type)
        {
            return type.IsPrimitive 
                || type.IsEnum
                || type == typeof (string)
                || type == typeof(DateTime);
        }

        protected PropertyDescriptor GetChainedPropertyDescriptorParent(Type type)
        {
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>)
                    || genericTypeDefinition == typeof(LinkValue<>))
                {
                    return TypeDescriptor.GetProperties(type).Find(@"Value", false);
                }

            }
            return null;
        }
        public Type GetWrappedValueType(Type type)
        {
            var propertyDescriptor = GetChainedPropertyDescriptorParent(type);
            if (propertyDescriptor == null)
            {
                return type;
            }
            return propertyDescriptor.PropertyType;
        }
        public object UnwrapValue(object value)
        {
            if (value == null)
            {
                return null;
            }
            var propertyDescriptor = GetChainedPropertyDescriptorParent(value.GetType());
            if (propertyDescriptor != null)
            {
                return propertyDescriptor.GetValue(value);
            }
            return value;
        }

        public virtual int Compare(object o1, object o2)
        {
            if (o1 == o2)
            {
                return 0;
            }
            o1 = UnwrapLinkValue(o1);
            o2 = UnwrapLinkValue(o2);
            return CollectionUtil.CompareColumnValues(o1, o2);
        }

        private static object UnwrapLinkValue(object o)
        {
            while (true)
            {
                var linkValue = o as ILinkValue;
                if (null == linkValue || ReferenceEquals(linkValue.Value, o))
                {
                    return o;
                }
                o = linkValue.Value;
            }
        }

        protected virtual ColumnCaption ColumnCaptionFromType(Type type)
        {
            var pdChain = GetChainedPropertyDescriptorParent(type);
            if (pdChain == null)
            {
                var displayNameAttribute =
                    type.GetCustomAttributes(typeof (InvariantDisplayNameAttribute), true)
                        .Cast<InvariantDisplayNameAttribute>().FirstOrDefault();
                if (displayNameAttribute != null && !displayNameAttribute.IsDefaultAttribute())
                {
                    return displayNameAttribute.ColumnCaption;
                }
                return new ColumnCaption(type.Name);
            }
            return ColumnCaptionFromType(pdChain.PropertyType);
        }
        public virtual bool IsRootTypeSelectable(Type type)
        {
            return typeof (ILinkValue).IsAssignableFrom(type);
        }
        private IColumnCaption GetBaseDisplayName(ColumnDescriptor columnDescriptor)
        {
            var oneToManyColumn = columnDescriptor.GetOneToManyColumn();
            if (oneToManyColumn != null)
            {
                var oneToManyAttribute = oneToManyColumn.GetAttributes().OfType<OneToManyAttribute>().FirstOrDefault();
                if (oneToManyAttribute != null)
                {
                    if (@"Key" == columnDescriptor.Name && oneToManyAttribute.IndexDisplayName != null)
                    {
                        return new ColumnCaption(oneToManyAttribute.IndexDisplayName);
                    }
                    if (@"Value" == columnDescriptor.Name && oneToManyAttribute.ItemDisplayName != null)
                    {
                        return new ColumnCaption(oneToManyAttribute.ItemDisplayName);
                    }
                }
            }
            var invariantDisplayNameAttribute = columnDescriptor.GetAttributes().OfType<InvariantDisplayNameAttribute>().FirstOrDefault();
            if (null != invariantDisplayNameAttribute)
            {
                return new ColumnCaption(invariantDisplayNameAttribute.InvariantDisplayName);
            }
            if (columnDescriptor.Name == null)
            {
                if (columnDescriptor.Parent != null)
                {
                    return GetBaseDisplayName(columnDescriptor.Parent);
                }
                if (columnDescriptor.PropertyType != null)
                {
                    return GetInvariantDisplayName(columnDescriptor.UiMode, columnDescriptor.PropertyType);
                }
            } 
            return new ColumnCaption(columnDescriptor.Name);
        }

        private IColumnCaption FormatChildDisplayName(ColumnDescriptor columnDescriptor, IColumnCaption childDisplayName)
        {
            if (null == columnDescriptor)
            {
                return childDisplayName;
            }
            var childDisplayNameAttribute =
                columnDescriptor.GetAttributes().OfType<ChildDisplayNameAttribute>().FirstOrDefault();
                
            if (null != childDisplayNameAttribute)
            {
                childDisplayName = new ColumnCaption(string.Format(childDisplayNameAttribute.InvariantFormat, childDisplayName.GetCaption(DataSchemaLocalizer.INVARIANT)));
            }
            return FormatChildDisplayName(columnDescriptor.Parent, childDisplayName);
        }

        public virtual IColumnCaption GetColumnCaption(ColumnDescriptor columnDescriptor)
        {
            DisplayNameAttribute displayNameAttribute =
                columnDescriptor.GetAttributes().OfType<DisplayNameAttribute>().FirstOrDefault();
            if (null != displayNameAttribute)
            {
                return ColumnCaption.UnlocalizableCaption(displayNameAttribute.DisplayName);
            }
            return FormatChildDisplayName(columnDescriptor.Parent, GetBaseDisplayName(columnDescriptor));
        }

        public virtual IColumnCaption GetInvariantDisplayName(string uiMode, Type type)
        {
            var invariantDisplayName = FilterAttributes(uiMode, type.GetCustomAttributes<InvariantDisplayNameAttribute>())
                .FirstOrDefault();
            if (invariantDisplayName != null)
            {
                return invariantDisplayName.ColumnCaption;
            }

            return new ColumnCaption(type.Name);
        }

        public virtual string GetTypeDescription(string uiMode, Type type)
        {
            return null;
        }

        public virtual IColumnCaption GetColumnCaption(string uiMode, Type type, string propertyName)
        {
            var columnDescriptor = ColumnDescriptor.RootColumn(this, type, uiMode).ResolveChild(propertyName);
            if (columnDescriptor == null)
            {
                return new ColumnCaption(propertyName);
            }

            return GetColumnCaption(columnDescriptor);
        }

        public string GetColumnCaption(ColumnCaption columnCaption, ColumnCaptionType columnCaptionType)
        {
            if (columnCaptionType == ColumnCaptionType.invariant)
            {
                return columnCaption.InvariantCaption;
            }
            return DataSchemaLocalizer.LookupColumnCaption(columnCaption);
        }

        public virtual bool IsHidden(ColumnDescriptor columnDescriptor)
        {
            if (IsObsolete(columnDescriptor))
            {
                return true;
            }
            var hiddenAttribute = FilterAttributes(columnDescriptor.UiMode, columnDescriptor.GetAttributes()).OfType<HiddenAttribute>().FirstOrDefault();
            if (hiddenAttribute != null)
            {
                return true;
            }

            var hideWhens = columnDescriptor.GetAttributes().OfType<HideWhenAttribute>().ToArray();
            var hideIfAncestor =
                new HashSet<Type>(hideWhens.SelectMany(attr => attr.AncestorsOfAnyOfTheseTypes).Where(type => null != type));
            if (hideIfAncestor.Count > 0)
            {
                for (ColumnDescriptor ancestor = columnDescriptor.Parent; ancestor != null; ancestor = ancestor.Parent)
                {
                    if (hideIfAncestor.Any(type => type.IsAssignableFrom(ancestor.PropertyType)))
                    {
                        return true;
                    }
                }
            }

            ColumnDescriptor oneToManyColumn = columnDescriptor.GetOneToManyColumn();
            if (oneToManyColumn != null)
            {
                var oneToManyAttribute = oneToManyColumn.GetAttributes().OfType<OneToManyAttribute>().FirstOrDefault();
                if (oneToManyAttribute != null && oneToManyAttribute.ForeignKey == columnDescriptor.Name)
                {
                    return true;
                }
            }
            return false;
        }

        public virtual bool IsObsolete(ColumnDescriptor columnDescriptor)
        {
            return columnDescriptor.GetAttributes().OfType<ObsoleteAttribute>().Any();
        }

        public string GetLocalizedColumnCaption(ColumnCaption columnCaption)
        {
            return DataSchemaLocalizer.LookupColumnCaption(columnCaption);
        }
        public DataSchemaLocalizer DataSchemaLocalizer { get; protected set; }

        public DataSchemaLocalizer GetDataSchemaLocalizer(ColumnCaptionType captionType)
        {
            return captionType == ColumnCaptionType.invariant ? DataSchemaLocalizer.INVARIANT : DataSchemaLocalizer;
        }

        public virtual String GetColumnDescription(ColumnDescriptor columnDescriptor)
        {
            PropertyDescriptor reflectedPropertyDescriptor = columnDescriptor.ReflectedPropertyDescriptor;
            if (reflectedPropertyDescriptor != null)
            {
                return reflectedPropertyDescriptor.Description;
            }
            return null;
        }

        public virtual IEnumerable<Attribute> GetAggregateAttributes(PropertyDescriptor originalPropertyDescriptor,
            AggregateOperation aggregateOperation)
        {
            var formatAttribute = GetFormatAttribute(originalPropertyDescriptor, aggregateOperation);
            if (formatAttribute != null)
            {
                yield return formatAttribute;
            }
        }

        protected FormatAttribute GetFormatAttribute(PropertyDescriptor originalPropertyDescriptor,
            AggregateOperation aggregateOperation)
        {
            if (aggregateOperation == AggregateOperation.Count)
            {
                return null;
            }
            if (aggregateOperation == AggregateOperation.Cv)
            {
                return new FormatAttribute(@"0.#%");
            }
            return (FormatAttribute) originalPropertyDescriptor.Attributes[typeof(FormatAttribute)];
        }

        public bool AttributeApplies(string uiMode, Attribute attribute)
        {
            var inUiModesAttribute = attribute as InUiModesAttribute;
            if (inUiModesAttribute != null)
            {
                if (!inUiModesAttribute.AppliesInUiMode(uiMode))
                {
                    return false;
                }
            }

            return true;
        }

        private int GetAttributeClassDepth(Attribute attribute)
        {
            var type = attribute.GetType();
            int depth = 0;
            while (type != null && type != typeof(Attribute))
            {
                type = type.BaseType;
                depth++;
            }

            return depth;
        }

        public IEnumerable<T> FilterAttributes<T>(string uiMode, IEnumerable<T> attributes) where T : Attribute
        {
            return attributes.Where(attr=>AttributeApplies(uiMode, attr)).OrderByDescending(GetAttributeClassDepth);
        }
    }
}
