using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AuditLogReflectionTests : AbstractUnitTest
    {
        [TestMethod]
        public void FindOverriddenTrackedProperties()
        {
            var trackedPropertyTypes = ListAllTrackedProperties().Select(p => p.PropertyType).ToHashSet();
            var checkedTypes = new HashSet<Type>{typeof(CustomIon)};
            foreach (var property in ListAllTrackedProperties())
            {
                var declaringType = property.DeclaringType;
                if (!checkedTypes.Add(declaringType))
                {
                    continue;
                }
                foreach (var baseType in GetAllBaseTypes(declaringType))
                {
                    if (trackedPropertyTypes.Contains(baseType))
                    {
                        Assert.Fail("Type {0} should not have any tracked properties, because {1} is used elsewhere", declaringType, baseType);
                    }
                }
            }
        }

        public IEnumerable<Assembly> ListAssemblies()
        {
            yield return typeof(SkylineWindow).Assembly;
            yield return typeof(RowItem).Assembly;
            yield return typeof(ImmutableList).Assembly;
        }

        public IEnumerable<PropertyInfo> ListAllTrackedProperties()
        {
            foreach (var assembly in ListAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var property in type.GetProperties())
                    {
                        if (property.GetCustomAttributes(typeof(TrackAttributeBase)).Any())
                        {
                            yield return property;
                        }
                    }
                }
            }
        }

        public IEnumerable<Type> GetAllBaseTypes(Type type)
        {
            var types = type.GetInterfaces().AsEnumerable();
            if (type.BaseType != null)
            {
                types = types.Append(type.BaseType).Concat(GetAllBaseTypes(type.BaseType));
            }

            return types;
        }
    }
}
