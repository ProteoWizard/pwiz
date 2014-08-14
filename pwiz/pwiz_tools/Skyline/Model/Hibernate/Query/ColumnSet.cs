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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NHibernate;
using NHibernate.Metadata;
using NHibernate.Type;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    /// <summary>
    /// Helps to display a column picker in a tree view where there is a many-to-one relationship
    /// from each parent node in the tree to its first child.
    /// Additionally, there's a one-to-many relationship from each parent to a child node
    /// called "Results".
    /// 
    /// After the user has chosen a number of columns, the columns are inspected to determine
    /// which table must be selected from to yield all of those columns.
    /// Choosing columns from further down the tree usually results in more rows in the ResultSet.
    /// </summary>
    public class ColumnSet
    {
        class Table
        {
            public String Name { get; set; }
            public Type PersistentClass { get; set; }
            public Type ResultsClass { get; set; }
            public Type ResultsSummaryClass { get; set; }
            public ParentRelationship Parent { get; set; }
        }

        class ParentRelationship
        {
            public Table ParentTable { get; set; }
            public String ParentName { get; set; }
            public String ChildrenName { get; set; }
        }


        public Schema Schema { get; set;}
        /// <summary>
        /// The table which is the deepest child in the tree.  There are many-to-one relationships
        /// from the MostManyTable all the way back to the table that is displayed at the root 
        /// of the tree.
        /// </summary>
        private Table MostManyTable { get; set;}

        /// <summary>
        /// Returns a ColumnSet for the DbTransitions table.  The column picker has DbProtein
        /// at the root, and allows the user to drill down all the way to the DbTransitions table
        /// at the bottom.
        /// </summary>
        public static ColumnSet GetTransitionsColumnSet(Schema schema)
        {
            return new ColumnSet
                       {
                           Schema = schema,
                           MostManyTable = GetTransitionsTable()
                       };
        }

        private static Table GetTransitionsTable()
        {
            Table protein = new Table
                                {
                                    Name = "Protein", // Not L10N
                                    PersistentClass = typeof(DbProtein),
                                    ResultsClass = typeof(DbProteinResult)
                                };
            Table peptide = new Table
                                {
                                    Name = "Peptide", // Not L10N
                                    PersistentClass = typeof (DbPeptide),
                                    ResultsClass = typeof (DbPeptideResult),
                                    Parent = new ParentRelationship
                                                 {
                                                     ChildrenName = "Peptides", // Not L10N
                                                     ParentName = "Protein", // Not L10N
                                                     ParentTable = protein
                                                 }
                                };
            Table precursor = new Table
                                  {
                                      Name = "Precursor", // Not L10N
                                      PersistentClass = typeof (DbPrecursor),
                                      ResultsClass = typeof (DbPrecursorResult),
                                      ResultsSummaryClass = typeof (DbPrecursorResultSummary),
                                      Parent = new ParentRelationship
                                                   {
                                                       ChildrenName = "Precursors", // Not L10N
                                                       ParentName = "Peptide", // Not L10N
                                                       ParentTable = peptide
                                                   }
                                  };
            Table transition = new Table
                                   {
                                       Name = "Transition", // Not L10N
                                       PersistentClass = typeof (DbTransition),
                                       ResultsClass = typeof (DbTransitionResult),
                                       ResultsSummaryClass = typeof (DbTransitionResultSummary),
                                       Parent = new ParentRelationship
                                                    {
                                                        ChildrenName = "Transitions", // Not L10N
                                                        ParentName = "Precursor", // Not L10N
                                                        ParentTable = precursor
                                                    }
                                   };
            return transition;
        }

        /// <summary>
        /// Return the TreeNodes that should be displayed in the column picker UI.
        /// </summary>
        public List<TreeNode> GetTreeNodes()
        {
            Table table = MostManyTable;
            TreeNode childNode = null;
            Identifier identifier = null;
            while (true)
            {
                List<TreeNode> nodes = new List<TreeNode>();
                if (childNode != null)
                {
                    nodes.Add(childNode);
                }
                nodes.AddRange(GetTreeNodes(table, identifier));
                if (table.Parent == null)
                {
                    foreach (TreeNode treeNode in nodes)
                    {
                        NodeData nodeData = treeNode.Tag as NodeData;
                        if (nodeData != null)
                        {
                            treeNode.Text = nodeData.Caption;
                        }
                    }
                    return nodes;
                }
                childNode = new TreeNode {Name = table.Parent.ChildrenName, Text = table.Parent.ChildrenName};
                childNode.Nodes.AddRange(nodes.ToArray());
                identifier = new Identifier(identifier, table.Parent.ParentName);
                table = table.Parent.ParentTable;
            }
        }

        private IEnumerable<TreeNode> GetTreeNodes(Table table, Identifier identifier)
        {
            List<TreeNode> treeNodes = new List<TreeNode>();
            IClassMetadata classMetadata = Schema.GetClassMetadata(table.PersistentClass);
            if (table.ResultsClass != null)
            {
                TreeNode resultsNode = new TreeNode { Name = "Results", Text = ResultText(table, "Results") }; // Not L10N
                foreach (TreeNode treeNode in GetTreeNodes(Schema.GetClassMetadata(table.ResultsClass), identifier))
                {
                    ((NodeData) treeNode.Tag).Results = true;
                    resultsNode.Nodes.Add(treeNode);
                }
                treeNodes.Add(resultsNode);
            }
            if (table.ResultsSummaryClass != null)
            {
                TreeNode resultsSummaryNode = new TreeNode
                                                  {
                                                      Name = "ResultsSummary", // Not L10N
                                                      Text = ResultText(table, "ResultsSummary") // Not L10N
                                                  }; 
                foreach (TreeNode treeNode in GetTreeNodes(Schema.GetClassMetadata(table.ResultsSummaryClass), identifier))
                {
                    ((NodeData)treeNode.Tag).ResultsSummary = true;
                    resultsSummaryNode.Nodes.Add(treeNode);
                }
                treeNodes.Add(resultsSummaryNode);
            }
            treeNodes.AddRange(GetTreeNodes(classMetadata, identifier));
            return treeNodes;
        }

        private static string ResultText(Table table, string resultText)
        {
            if (!ReferenceEquals(table.PersistentClass, typeof(DbProtein)))
                resultText = table.Name + resultText;
            return resultText;
        }

        protected List<TreeNode> GetTreeNodes(IClassMetadata classMetadata, Identifier identifier)
        {
            List<TreeNode> result = new List<TreeNode>();
            // Add special ratio names in order after the default ratio name
            int lastRatioIndex = -1;
            foreach (String propertyName in classMetadata.PropertyNames)
            {
                IType propertyType = classMetadata.GetPropertyType(propertyName);
                if (propertyType is ManyToOneType)
                {
                    continue;
                }
                var label = propertyName;
                bool isRatio = RatioPropertyAccessor.IsRatioOrRdotpProperty(label);
                if (isRatio)
                    label = RatioPropertyAccessor.GetDisplayName(label);
                else if (AnnotationDef.IsAnnotationProperty(label))
                    label = AnnotationDef.GetColumnDisplayName(label);
                else if (label.IndexOf("Ratio", StringComparison.Ordinal) != -1) // Not L10N: Label is only used in this file. Never displayed.
                    lastRatioIndex = result.Count;
                var columnInfo = CreateColumnInfo(identifier, classMetadata, propertyName);
                if (columnInfo.IsHidden)
                {
                    continue;
                }
                TreeNode propertyNode 
                    = new TreeNode
                    {
                        Name = propertyName,
                        Text = label,
                        Tag = columnInfo
                    };
                if (isRatio && lastRatioIndex != -1)
                    result.Insert(++lastRatioIndex, propertyNode);
                else
                    result.Add(propertyNode);
            }
            return result;
        }

        protected NodeData CreateColumnInfo(Identifier parentIdentifier, IClassMetadata classMetadata, String propertyName)
        {
            Type type = classMetadata.GetMappedClass(EntityMode.Poco);
            ColumnInfo columnInfo = Schema.GetColumnInfo(type, propertyName);
            NodeData nodeData = new NodeData
                                        {
                                            ReportColumn = new ReportColumn(type, new Identifier(parentIdentifier, propertyName)),
                                            Caption = columnInfo.Caption,
                                            Format = columnInfo.Format,
                                            ColumnType = columnInfo.ColumnType,
                                            IsHidden = columnInfo.IsHidden
                                        };
            return nodeData;
        }

        private static Identifier GetCommonPrefix(IList<NodeData> columnInfos)
        {
            if (columnInfos.Count == 0)
            {
                return null;
            }
            
            Identifier commonPrefix = columnInfos[0].ReportColumn.Column.Parent;
            for (int i = 1; i < columnInfos.Count; i++ )
            {
                Identifier identifier = columnInfos[i].ReportColumn.Column;
                while (commonPrefix != null && !identifier.StartsWith(commonPrefix))
                {
                    commonPrefix = commonPrefix.Parent;
                }
            }
            return commonPrefix;
        }

        public void ResolveColumn(NodeData nodeData, out Type table, out String column)
        {
            if (nodeData.Results)
            {
                Schema.Resolve(MostManyTable.ResultsClass, ToResultsIdentifier(nodeData.ReportColumn.Column), out table,
                               out column);
            }
            else if (nodeData.ResultsSummary)
            {
                Schema.Resolve(MostManyTable.ResultsSummaryClass, ToResultsSummaryIdentifier(nodeData.ReportColumn.Column), out table,
                               out column);
            }
            else
            {
                Schema.Resolve(MostManyTable.PersistentClass, nodeData.ReportColumn.Column, out table, out column);
            }
        }

        /// <summary>
        /// There are many-to-one relationships between each of the XxxResults table
        /// to the parent results row.  For instance, there's a "PeptideResult" column
        /// on the "DbPrecursorResults" class.  This class takes something like:
        /// Precursor.Peptide.Foo
        /// and transforms it to:
        /// PrecursorResult.PeptideResult.Foo
        /// so that it refers to a real column on the "DbTransitionResult" table.
        /// </summary>
        public static Identifier ToResultsIdentifier(Identifier identifier)
        {
            return ToResultsIdentifier(identifier, "Result"); // Not L10N
        }

        public static Identifier ToResultsSummaryIdentifier(Identifier identifier)
        {
            return ToResultsIdentifier(identifier, "ResultSummary"); // Not L10N
        }

        private static Identifier ToResultsIdentifier(Identifier identifier, string suffix)
        {
            List<String> parts = new List<String>();
            for (int i = 0; i < identifier.Parts.Count - 1; i++)
            {
                parts.Add(identifier.Parts[i] + suffix);
            }
            parts.Add(identifier.Parts[identifier.Parts.Count - 1]);
            return new Identifier(parts);
        }

        /// <summary>
        /// Finds a NodeData element searching the entire tree.
        /// </summary>
        private static NodeData FindNodeData(TreeView root, TableType type, Identifier reportColumn)
        {
            foreach (TreeNode node in root.Nodes)
            {
                NodeData nodeData = FindNodeData(node, type, reportColumn);
                if (nodeData != null)
                {
                    return nodeData;
                }
            }
            return null;
        }

        /// <summary>
        /// Walks the tree to find the NodeData.
        /// </summary>
        private static NodeData FindNodeData(TreeNode root, TableType type, Identifier identifier)
        {
            NodeData nodeData = root.Tag as NodeData;
            if (nodeData != null)
            {
                if (nodeData.Results)
                {
                    if (type == TableType.result && Equals(identifier, ToResultsIdentifier(nodeData.ReportColumn.Column)))
                        return nodeData;
                }
                else if (nodeData.ResultsSummary)
                {
                    if (type == TableType.summary && Equals(identifier, ToResultsSummaryIdentifier(nodeData.ReportColumn.Column)))
                        return nodeData;
                }
                else if (type == TableType.node && Equals(nodeData.ReportColumn.Column, identifier))
                {
                    return nodeData;
                }
            }
            foreach (TreeNode child in root.Nodes)
            {
                NodeData result = FindNodeData(child, type, identifier);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Used when loading a saved Report, this method figures out what to display in the UI.
        /// The saved Report just has the information necessary to execute the query, which 
        /// is the set of columns to query, and paired with the tables to query.
        /// This then needs to be transformed back into which columns in the big tree view were
        /// selected, and whether the "Pivot Results" checkbox was checked.
        /// </summary>
        public void GetColumnInfos(Report report, TreeView treeView, out List<NodeData> columnInfos)
        {
            // CONSIDER: Why have the base class?
            if (!(report is SimpleReport))
            {
                throw new InvalidOperationException(Resources.ColumnSet_GetColumnInfos_Unexpected_report_type);
            }
            SimpleReport simpleReport = (SimpleReport) report;
            columnInfos = new List<NodeData>();
            var allColumns = new List<ReportColumn>(simpleReport.Columns);
            var pivotReport = simpleReport as PivotReport;
            if (pivotReport != null)
            {
                allColumns.AddRange(pivotReport.CrossTabValues);
            }
            if (allColumns.Count == 0)
            {
                return;
            }
            var allTables = new HashSet<Type>(from reportColumn in allColumns
                                              select reportColumn.Table);

            Table table = MostManyTable;
            Identifier prefix = null;
            while (table != null)
            {
                if (allTables.Contains(table.PersistentClass) ||
                    allTables.Contains(table.ResultsClass) ||
                    allTables.Contains(table.ResultsSummaryClass))
                {
                    break;
                }
                if (table.Parent == null)
                {
                    string tableNames = string.Join(", ", (from reportTable in allTables // Not L10N
                                                           select reportTable.ToString()).ToArray());
                    throw new InvalidDataException(string.Format(Resources.ColumnSet_GetColumnInfos_Unable_to_find_table_for__0_, tableNames));
                }
                table = table.Parent.ParentTable;
                prefix = new Identifier(prefix, table.Name);
            }

            foreach (var unqualifiedId in allColumns)
            {
                Identifier identifier;
                TableType type = ReportColumn.GetTableType(unqualifiedId.Table);
                switch (type)
                {
                    case TableType.result:
                        identifier = JoinIds(prefix, unqualifiedId.Column, ToResultsIdentifier);
                        break;
                    case TableType.summary:
                        identifier = JoinIds(prefix, unqualifiedId.Column, ToResultsSummaryIdentifier);
                        break;
                    default:
                        identifier = JoinIds(prefix, unqualifiedId.Column);
                        break;
                }

                columnInfos.Add(FindNodeData(treeView, type, identifier));
            }
        }

        private static Identifier JoinIds(Identifier prefix, Identifier suffix)
        {
            return new Identifier(prefix, suffix);
        }

        private static Identifier JoinIds(Identifier prefix, Identifier suffix,
            Func<Identifier, Identifier> modPrefix)
        {
            return new Identifier(modPrefix(new Identifier(prefix, suffix.Parts[0])), suffix.RemovePrefix(1));
        }

        private SimpleReport Pivot(SimpleReport simpleReport, PivotType pivotType)
        {
            var crossTabHeaders = pivotType == null 
                ? new ReportColumn[0] 
                : pivotType.GetCrosstabHeaders(simpleReport.Columns);
            var groupByColumns = pivotType == null 
                ? new ReportColumn[0]
                : pivotType.GetGroupByColumns(simpleReport.Columns);

            foreach (ReportColumn id in crossTabHeaders)
            {
                groupByColumns.Remove(id);
            }
            var normalColumns = new List<ReportColumn>();
            var crossTabColumns = new List<ReportColumn>();
            foreach (ReportColumn id in simpleReport.Columns)
            {
                Type table;
                String column;
                Schema.Resolve(id.Table, id.Column, out table, out column);
                if (pivotType != null && pivotType.IsCrosstabValue(table, column))
                {
                    crossTabColumns.Add(id);
                }
                else
                {
                    normalColumns.Add(id);
                }
            }
            if (crossTabColumns.Count == 0)
            {
                return simpleReport;
            }
            return new PivotReport
                       {
                           Columns = normalColumns,
                           GroupByColumns = new List<ReportColumn>(groupByColumns),
                           CrossTabHeaders = crossTabHeaders,
                           CrossTabValues = crossTabColumns
                       };
        }

        /// <summary>
        /// Returns a Report for the given set of columns.
        /// The columns are inspected to see which table has to be queried.
        /// Also, a PivotReport will be returned if any columns from a Results table were
        /// selected, and pivotResults is true.
        /// </summary>
        public Report GetReport(List<NodeData> columnInfos, PivotType pivotType)
        {
            // Get the common prefix, and the ancestor table it represents
            Identifier commonPrefix = GetCommonPrefix(columnInfos);
            Table table = MostManyTable;
            int prefixLength = 0;
            if (commonPrefix != null)
            {
                prefixLength = commonPrefix.Parts.Count;
                for (int i = 0; i < prefixLength; i++ )
                {
                    table = table.Parent.ParentTable;
                }
            }
            // Calculate the list of ReportColumns from the NodeData
            var displayColumns = new List<ReportColumn>();
            foreach(NodeData columnInfo in columnInfos)
            {
                Identifier identifier = columnInfo.ReportColumn.Column.RemovePrefix(prefixLength);
                if (columnInfo.Results)
                {
                    displayColumns.Add(new ReportColumn(table.ResultsClass, ToResultsIdentifier(identifier)));
                }
                else if (columnInfo.ResultsSummary)
                {
                    displayColumns.Add(new ReportColumn(table.ResultsSummaryClass, ToResultsSummaryIdentifier(identifier)));
                }
                else
                {
                    displayColumns.Add(new ReportColumn(table.PersistentClass, identifier));
                }
            }
            SimpleReport simpleReport = new SimpleReport
                           {
                               Columns = displayColumns
                           };
            return Pivot(simpleReport, pivotType);
        }
    }

    /// <summary>
    /// Tree node tag data.
    /// </summary>
    public class NodeData : ColumnInfo
    {
        public bool Results { get; set; }
        public bool ResultsSummary { get; set; }
    }
}
