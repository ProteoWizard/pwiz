using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.DocSettings
{
    public class Reflector<T> where T : class
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly List<Property> _properities;

        static Reflector()
        {
            _properities = new List<Property>();
            foreach (var property in typeof(T).GetProperties())
            {
                var attributes = property.GetCustomAttributes(false);
                var diffAttr = attributes.OfType<DiffAttributeBase>().FirstOrDefault();

                if (diffAttr != null)
                {
                    _properities.Add(new Property(property, diffAttr));
                }
            }
        }

        public static List<PropertyDiff> CreateDiff(T oldObj, T newObj)
        {
            return CreateDiff(oldObj, newObj, PropertyPath.Root);
        }

        private static List<PropertyDiff> CreateDiff(T oldObj, T newObj, PropertyPath path)
        {
            var result = new List<PropertyDiff>();
            var time = DateTime.Now;

            foreach (var property in _properities)
            {
                var oldVal = property.PropertyInfo.GetValue(oldObj);
                var newVal = property.PropertyInfo.GetValue(newObj);

                var newPath = path.Property(property.Name);

                if (property.Diff)
                {
                    var collectionInfo = CollectionInfo.ForType(property.PropertyInfo.PropertyType);
                    if (collectionInfo != null)
                    {
                        result.AddRange(CompareCollections(collectionInfo, time, oldVal, newVal, property, newPath,
                            (prop, propertyPath, oldElem, newElem) =>
                            {
                                var res = new List<PropertyDiff>();

                                if (!object.Equals(oldElem, newElem))
                                {
                                    res.Add(new PropertyValueChangedDiff(prop, propertyPath, time, oldElem, newElem));
                                }

                                return res;
                            }));
                    }
                    else if (!object.Equals(oldVal, newVal))
                    {
                        result.Add(new PropertyValueChangedDiff(property, newPath, time, oldVal, newVal));
                    }
                }
                else if (!ReferenceEquals(oldVal, newVal))
                {
                    var propertyType = property.PropertyInfo.PropertyType;
                    var collectionInfo = CollectionInfo.ForType(propertyType);
                    var elemType = collectionInfo == null ? propertyType : collectionInfo.ElementType;

                    var type = typeof(Reflector<>).MakeGenericType(elemType);
                    var createDiff = type.GetMethod("CreateDiff", BindingFlags.Static | BindingFlags.NonPublic); // Not L10N
                    var reflector = Activator.CreateInstance(type);

                    if (createDiff != null)
                    {
                        if (collectionInfo != null)
                        {
                            result.AddRange(CompareCollections(collectionInfo, time, oldVal, newVal, property, newPath,
                                (prop, propertyPath, oldElem, newElem) => (List<PropertyDiff>) createDiff.Invoke(
                                    reflector,
                                    new[]
                                    {
                                        oldElem, newElem,
                                        propertyPath
                                    })));
                        }
                        else
                        {
                            result.AddRange((List<PropertyDiff>)createDiff.Invoke(reflector, new[] { oldVal, newVal, newPath }));
                        }
                    }
                }
            }

            return result;
        }

        private static IEnumerable<PropertyDiff> CompareCollections(ICollectionInfo collectionInfo, DateTime time, object oldVal, object newVal, Property property, PropertyPath newPath, Func<Property, PropertyPath, object, object, List<PropertyDiff>> func)
        {
            var result = new List<PropertyDiff>();

            var oldKeys = collectionInfo.GetKeys(oldVal).OfType<object>().ToArray();
            var newKeys = collectionInfo.GetKeys(newVal).OfType<object>().ToList();

            var removed = new List<KeyValuePair<object, object>>();
            var added   = new List<KeyValuePair<object, object>>();

            foreach(var key in oldKeys)
            {
                var index = newKeys.IndexOf(key);
                if (index >= 0)
                {
                    var oldElem = collectionInfo.GetItemValueFromKey(oldVal, key);
                    var newElem = collectionInfo.GetItemValueFromKey(newVal, key);

                    result.AddRange(func(property, newPath.LookupByKey(key.ToString()), oldElem, newElem));
                    newKeys.RemoveAt(index);
                }
                else
                {
                    removed.Add(new KeyValuePair<object, object>(key, collectionInfo.GetItemValueFromKey(oldVal, key)));
                }
            }

            added.AddRange(newKeys.Select(k => new KeyValuePair<object, object>(k, collectionInfo.GetItemValueFromKey(newVal, k))));

            if (removed.Any())
            {
                result.Add(new CollectionElementsRemovedDiff(property, newPath, time, removed));
            }

            if (added.Any())
            {
                result.Add(new CollectionElementsAddedDiff(property, newPath, time, added));
            }

            return result;
        }

        public static bool Equals(T a, T b)
        {
            return !CreateDiff(a, b).Any();
        }

        public new static bool Equals(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.GetType() != b.GetType() || a.GetType() != typeof(T)) return false;

            return Equals((T) a, (T) b);
        }

        public static int GetHashCode(object obj)
        {
            var hashCode = 0;

            foreach (var property in _properities)
            {
                var value = property.PropertyInfo.GetValue(obj);
                hashCode *= 397;
                if (value != null)
                    hashCode ^= value.GetHashCode();
            }

            return hashCode;
        }
    }
}