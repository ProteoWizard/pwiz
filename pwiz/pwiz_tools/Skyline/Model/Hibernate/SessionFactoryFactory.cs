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
                configuration.SetProperty("hbm2ddl.auto", "create-drop"); // Not L10N
            }
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }

        public static Configuration GetConfiguration(String path, SrmSettings settings)
        {
            Configuration configuration = new Configuration()
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName) // Not L10N
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder // Not L10N
                {
                    DataSource = path
                }.ToString())
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName); // Not L10N
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            configuration.SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName); // Not L10N
            configuration.AddInputStream(assembly.GetManifestResourceStream(typeof(SessionFactoryFactory).Namespace + ".mapping.xml")); // Not L10N
            if (settings != null)
                AddRatioColumns(configuration, settings);
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.protein, typeof(DbProtein));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.peptide, typeof(DbPeptide));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.precursor, typeof(DbPrecursor));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.transition, typeof(DbTransition));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.replicate, typeof(DbProteinResult));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.precursor_result, typeof(DbPrecursorResult));
            AddAnnotations(configuration, settings, AnnotationDef.AnnotationTarget.transition_result, typeof(DbTransitionResult));
            return configuration;
        }

        private const string STRING_TYPE_NAME = "string"; // Not L10N
        private const string BOOL_TYPE_NAME = "bool"; // Not L10N
        private const string NDOUBLE_TYPE_NAME = "double?"; // Not L10N

        private static readonly Dictionary<Type, string> DICT_TYPE_TO_NAME =
            new Dictionary<Type, string>
                {
                    {typeof(AnnotationPropertyAccessor), STRING_TYPE_NAME},
                    {typeof(BoolAnnotationPropertyAccessor), BOOL_TYPE_NAME},
                    {typeof(NumberAnnotationPropertyAccessor), NDOUBLE_TYPE_NAME},
                    {typeof(RatioPropertyAccessor), NDOUBLE_TYPE_NAME}
                };

        private static void AddRatioColumns(Configuration configuration, SrmSettings settings)
        {
            var mappingPeptide = configuration.GetClassMapping(typeof(DbPeptideResult));
            var mappingPrec = configuration.GetClassMapping(typeof(DbPrecursorResult));
            var mappingTran = configuration.GetClassMapping(typeof(DbTransitionResult));

            var mods = settings.PeptideSettings.Modifications;
            var standardTypes = mods.RatioInternalStandardTypes;
            var labelTypes = mods.GetModificationTypes().ToArray();
            if (labelTypes.Length > 2)
            {
                foreach (var standardType in standardTypes)
                {
                    foreach (var labelType in labelTypes)
                    {
                        if (ReferenceEquals(labelType, standardType))
                            continue;

                        AddColumn(mappingPeptide,
                                  RatioPropertyAccessor.PeptideRatioProperty(labelType, standardType).ColumnName,
                                  typeof (RatioPropertyAccessor));
                        AddColumn(mappingPeptide,
                                  RatioPropertyAccessor.PeptideRdotpProperty(labelType, standardType).ColumnName,
                                  typeof (RatioPropertyAccessor));
                    }

                    // Only add TotalAreaRatioTo<label type> and AreaRatioTo<label type> columns
                    // when there is more than one internal standard label type, because that
                    // is the only time that data is added to these columns in the database.
                    if (standardTypes.Count > 1)
                    {
                        AddColumn(mappingPrec, RatioPropertyAccessor.PrecursorRatioProperty(standardType).ColumnName,
                                  typeof (RatioPropertyAccessor));
                        AddColumn(mappingPrec, RatioPropertyAccessor.PrecursorRdotpProperty(standardType).ColumnName,
                                  typeof (RatioPropertyAccessor));
                        AddColumn(mappingTran, RatioPropertyAccessor.TransitionRatioProperty(standardType).ColumnName,
                                  typeof (RatioPropertyAccessor));
                    }
                }
            }

            if (settings.HasGlobalStandardArea)
            {
                foreach (var labelType in labelTypes)
                {
                    AddColumn(mappingPeptide, RatioPropertyAccessor.PeptideRatioProperty(labelType, null).ColumnName, typeof(RatioPropertyAccessor));
                }
                AddColumn(mappingPrec, RatioPropertyAccessor.PrecursorRatioProperty(null).ColumnName, typeof(RatioPropertyAccessor));
                AddColumn(mappingTran, RatioPropertyAccessor.TransitionRatioProperty(null).ColumnName, typeof(RatioPropertyAccessor));
            }
        }

        private static void AddAnnotations(Configuration configuration, SrmSettings settings, AnnotationDef.AnnotationTarget annotationTarget, Type persistentClass)
        {
            var mapping = configuration.GetClassMapping(persistentClass);
            foreach (var annotationDef in settings.DataSettings.AnnotationDefs)
            {
                if (!annotationDef.AnnotationTargets.Contains(annotationTarget))
                {
                    continue;
                }
                string columnName = AnnotationDef.GetColumnName(annotationDef.Name);
                Type accessorType;
                switch (annotationDef.Type)
                {
                    case AnnotationDef.AnnotationType.number:
                        accessorType = typeof (NumberAnnotationPropertyAccessor);
                        break;
                    case AnnotationDef.AnnotationType.true_false:
                        accessorType = typeof (BoolAnnotationPropertyAccessor);
                        break;
                    default:
                        accessorType = typeof (AnnotationPropertyAccessor);
                        break;
                }
                
                AddColumn(mapping, columnName, accessorType);
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
