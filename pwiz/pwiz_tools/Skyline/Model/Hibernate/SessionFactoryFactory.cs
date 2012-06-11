/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Hibernate
{
    public class SessionFactoryFactory
    {
        public static ISessionFactory CreateSessionFactory(String path, bool createSchema)
        {
            Configuration configuration = GetConfiguration(path, null);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create-drop");
            }
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }

        public static Configuration GetConfiguration(String path, SrmSettings settings)
        {
            Configuration configuration = new Configuration()
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                {
                    DataSource = path
                }.ToString())
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName);
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            configuration.SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            configuration.AddInputStream(assembly.GetManifestResourceStream(typeof(SessionFactoryFactory).Namespace + ".mapping.xml"));
            if (settings != null)
                AddRatioColumns(configuration, settings);
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.protein, typeof(DbProtein));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.peptide, typeof(DbPeptide));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.precursor, typeof(DbPrecursor));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.transition, typeof(DbTransition));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.precursor_result, typeof(DbPrecursorResult));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.transition_result, typeof(DbTransitionResult));
            return configuration;
        }

        private const string STRING_TYPE_NAME = "string";
        private const string BOOL_TYPE_NAME = "bool";
        private const string NDOUBLE_TYPE_NAME = "double?";

        private static readonly Dictionary<Type, string> DICT_TYPE_TO_NAME =
            new Dictionary<Type, string>
                {
                    {typeof(AnnotationPropertyAccessor), STRING_TYPE_NAME},
                    {typeof(BoolAnnotationPropertyAccessor), BOOL_TYPE_NAME},
                    {typeof(RatioPropertyAccessor), NDOUBLE_TYPE_NAME}
                };

        private static void AddRatioColumns(Configuration configuration, SrmSettings settings)
        {
            var mods = settings.PeptideSettings.Modifications;
            var standardTypes = mods.InternalStandardTypes;
            var labelTypes = mods.GetModificationTypes().ToArray();
            if (labelTypes.Length < 3)
                return;

            var mappingPeptide = configuration.GetClassMapping(typeof(DbPeptideResult));
            var mappingPrec = configuration.GetClassMapping(typeof(DbPrecursorResult));
            var mappingTran = configuration.GetClassMapping(typeof(DbTransitionResult));

            foreach (var standardType in standardTypes)
            {
                foreach (var labelType in labelTypes)
                {
                    if (ReferenceEquals(labelType, standardType))
                        continue;

                    string namePep = RatioPropertyAccessor.GetPeptideColumnName(labelType, standardType);
                    AddColumn(mappingPeptide, namePep, typeof(RatioPropertyAccessor));
                }

                // Only add TotalAreaRatioTo<label type> and AreaRatioTo<label type> columns
                // when there is more than one internal standard label type, because that
                // is the only time that data is added to these columns in the database.
                if (standardTypes.Count > 1)
                {
                    string namePrec = RatioPropertyAccessor.GetPrecursorColumnName(standardType);
                    AddColumn(mappingPrec, namePrec, typeof(RatioPropertyAccessor));
                    string nameTran = RatioPropertyAccessor.GetTransitionColumnName(standardType);
                    AddColumn(mappingTran, nameTran, typeof(RatioPropertyAccessor));
                }
            }
        }

        private static void AddAnnotations(Configuration configuration, AnnotationDef.AnnotationTarget annotationTarget, Type persistentClass)
        {
            var mapping = configuration.GetClassMapping(persistentClass);
            foreach (var annotationDef in Properties.Settings.Default.AnnotationDefList)
            {
                if (0 == (annotationDef.AnnotationTargets & annotationTarget))
                {
                    continue;
                }
                string columnName = AnnotationDef.GetColumnName(annotationDef.Name);
                var isBoolAttribute = annotationDef.Type == AnnotationDef.AnnotationType.true_false;
                AddColumn(mapping, columnName, isBoolAttribute ?
                    typeof(BoolAnnotationPropertyAccessor) : typeof(AnnotationPropertyAccessor));
            }
        }

        private static void AddColumn(PersistentClass mapping, string columnName, Type accessorType)
        {
            var typeName = DICT_TYPE_TO_NAME[accessorType];
            bool isNullable = typeName[typeName.Length - 1] == '?';
            if (isNullable)
                typeName = typeName.Substring(0, typeName.Length - 1);

            // String annotations can be null also
            isNullable = isNullable || Equals(STRING_TYPE_NAME, typeName);

            var column = new Column(columnName) {IsNullable = isNullable};
            mapping.Table.AddColumn(column);
            var value = new SimpleValue(mapping.Table) {TypeName = typeName};
            value.AddColumn(column);
            var property = new Property(value)
            {
                Name = columnName,                
                PropertyAccessorName = accessorType.AssemblyQualifiedName
            };
            mapping.AddProperty(property);
        }
    }
}
