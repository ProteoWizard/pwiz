using System;
using System.Reflection;
using System.Reflection.Emit;

namespace pwiz.Common.SystemUtil
{
    public class DynamicTypeDefiner
    {
        private AssemblyBuilder _assembly;
        private ModuleBuilder _moduleBuilder;
        public DynamicTypeDefiner(string name)
        {
            var assemblyName = new AssemblyName() {Name = name};
            _assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName,
                AssemblyBuilderAccess.ReflectionOnly);
            _moduleBuilder = _assembly.DefineDynamicModule(name);
        }

        public Type GetType(string typeName)
        {
            return _assembly.GetType(typeName);
        }

        public Type DefineType(string typeName, Type baseType)
        {
            var typeBuilder = _moduleBuilder.DefineType(typeName);
            typeBuilder.SetParent(baseType);
            return typeBuilder.CreateType();
        }

        public static string MakeValidIdentifier(string listName)
        {
            return listName.Replace("\\", "\\\\")
                .Replace(",", "\\,")
                .Replace("+", "\\+")
                .Replace("&", "\\&")
                .Replace("*", "\\*")
                .Replace("[", "\\[")
                .Replace("]", "\\]")
                .Replace(".", "\\.")
                .Replace("|", "\\|");
        }
    }
}
