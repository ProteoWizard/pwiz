using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class ModelProperty
    {
        private static readonly object[] IndexArgs = new object[0];
        public ModelProperty(
            Func<object,object> modelGetter, 
            Action<object,object> modelSetter, 
            Func<object,object> entityGetter, 
            Action<object,object> entitySetter)
        {
            ModelGetter = modelGetter;
            ModelSetter = modelSetter;
            EntityGetter = entityGetter;
            EntitySetter = entitySetter;
        }

        public static ModelProperty Property<M,E>(String propertyName)
        {
            return Property<M, E>(propertyName, propertyName);
        }

        public static ModelProperty Property<M,E,T>(Func<M,T> getter, Action<M,T> setter, String propertyName)
        {
            var entityProperty = typeof (E).GetProperty(propertyName);
            return new ModelProperty(
                o => getter.Invoke((M)o), 
                (m, v) => setter.Invoke((M)m, (T)v), 
                e => entityProperty.GetValue(e, IndexArgs),
                (e, v) => entityProperty.SetValue(e, v, IndexArgs));
        }
        public static ModelProperty Property<M,E,T>(Func<M,T> getter, Action<M,T> setter, Func<E,T> entityGetter, Action<E,T> entitySetter)
        {
            return new ModelProperty(
                o => getter.Invoke((M)o),
                (m,v)=>setter.Invoke((M)m, (T)v),
                e => entityGetter.Invoke((E)e),
                (e,v)=>entitySetter.Invoke((E)e,(T)v)
                );
        }

        public static ModelProperty Property<M,E>(String modelPropertyName, String entityPropertyName)
        {
            var modelProperty = typeof (M).GetProperty(modelPropertyName);
            var entityProperty = typeof (E).GetProperty(entityPropertyName);
            return new ModelProperty(
                m => modelProperty.GetValue(m, IndexArgs),
                (m, v) => modelProperty.SetValue(m, v, IndexArgs),
                e => entityProperty.GetValue(e, IndexArgs),
                (e, v) => entityProperty.SetValue(e, v, IndexArgs));
        }

        public bool IsDirty(object model, object entity)
        {
            if (entity == null)
            {
                return false;
            }
            return !EqualValues(ModelGetter(model), EntityGetter(entity));
        }

        public static bool EqualValues<V>(V v1, V v2)
        {
            if (v1 is byte[] && v2 is byte[])
            {
                return Lists.EqualsDeep((IList) v1, (IList) v2);
            }
            return Equals(v1, v2);
        }

        public Func<object, object> ModelGetter { get; private set; }
        public Action<object, object> ModelSetter { get; private set; }
        public Func<object, object> EntityGetter { get; private set; }
        public Action<object, object> EntitySetter { get; private set; }
    }
}
