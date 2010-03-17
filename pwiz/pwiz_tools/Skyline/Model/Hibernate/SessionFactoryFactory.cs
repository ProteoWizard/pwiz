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
using System.Data.SQLite;
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
            Configuration configuration = GetConfiguration(path);
            if (createSchema)
            {
                configuration.SetProperty("hbm2ddl.auto", "create-drop");
            }
            ISessionFactory sessionFactory = configuration.BuildSessionFactory();
            return sessionFactory;
        }

        public static Configuration GetConfiguration(String path)
        {
            Configuration configuration = new Configuration()
                .SetProperty("dialect", typeof(NHibernate.Dialect.SQLiteDialect).AssemblyQualifiedName)
                .SetProperty("proxyfactory.factory_class", typeof(NHibernate.ByteCode.Castle.ProxyFactoryFactory).AssemblyQualifiedName)
                .SetProperty("connection.connection_string", new SQLiteConnectionStringBuilder
                {
                    DataSource = path
                }.ToString())
                .SetProperty("connection.driver_class", typeof(NHibernate.Driver.SQLite20Driver).AssemblyQualifiedName);
            Assembly assembly = typeof(SessionFactoryFactory).Assembly;
            configuration.SetProperty("connection.provider", typeof(NHibernate.Connection.DriverConnectionProvider).AssemblyQualifiedName);
            configuration.AddInputStream(assembly.GetManifestResourceStream(typeof(SessionFactoryFactory).Namespace + ".mapping.xml"));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.protein, typeof(DbProtein));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.peptide, typeof(DbPeptide));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.precursor, typeof(DbPrecursor));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.transition, typeof(DbTransition));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.precursor_result, typeof(DbPrecursorResult));
            AddAnnotations(configuration, AnnotationDef.AnnotationTarget.transition_result, typeof(DbTransitionResult));
            return configuration;
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
                var column = new Column(AnnotationPropertyAccessor.AnnotationPrefix + annotationDef.Name);
                mapping.Table.AddColumn(column);
                var isBoolAttribute = annotationDef.Type == AnnotationDef.AnnotationType.true_false;
                var value = new SimpleValue(mapping.Table)
                                {
                                        TypeName =
                                        isBoolAttribute ? "boolean" : "string",
                                };
                value.AddColumn(column);
                var property = new Property(value)
                                   {
                                       Name = AnnotationPropertyAccessor.AnnotationPrefix + annotationDef.Name,
                                       PropertyAccessorName = 
                                            isBoolAttribute 
                                            ? typeof(BoolAnnotationPropertyAccessor).AssemblyQualifiedName 
                                            : typeof(AnnotationPropertyAccessor).AssemblyQualifiedName,
                                   };
                mapping.AddProperty(property);
            }
        }
    }
}
