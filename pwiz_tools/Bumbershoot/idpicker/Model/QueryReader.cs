//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections;
using System.Collections.Generic;

namespace IDPicker
{

    public class QueryPage
    {
        /// <summary>Constructs a QueryPage from a page index and a list of rows (objects).</summary>
        public QueryPage (int pageIndex, IList rows)
        {
            PageIndex = pageIndex;
            Rows = rows;
        }

        /// <summary>Gets the page index.</summary>
        public int PageIndex { get; private set; }

        /// <summary>Gets the list of rows (objects).</summary>
        public IList Rows { get; private set; }
    }

    public interface IQueryReader
    {
        /// <summary>Gets the maximum number of rows in a QueryPage.</summary>
        int PageSize { get; }

        /// <summary>Gets the number of pages depending on PageSize and RowCount.</summary>
        int PageCount { get; }

        /// <summary>Gets the total number of rows for the query.</summary>
        int RowCount { get; }

        /// <summary>Gets a QueryPage for the specified page index.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        QueryPage GetPage (int pageIndex);

        /// <summary>Gets a QueryPage for the specified row index.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        QueryPage GetPageForRow (int rowIndex);
    }

    public abstract class QueryReader : IQueryReader
    {
        /// <summary>Gets or sets the maximum number of rows in a QueryPage.</summary>
        public virtual int PageSize
        {
            get { return pageSize; }
            set
            {
                pageSize = value;
                resetPages();
            }
        }

        /// <summary>Gets the number of pages depending on PageSize and RowCount.</summary>
        public virtual int PageCount { get; protected set; }

        /// <summary>Gets the total number of rows for the query.</summary>
        public virtual int RowCount { get; protected set; }

        /// <summary>Gets a QueryPage for the specified page index.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public abstract QueryPage GetPage (int pageIndex);

        /// <summary>Gets a QueryPage for the specified row index.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public virtual QueryPage GetPageForRow (int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= RowCount)
                throw new ArgumentOutOfRangeException();

            int pageIndex = (int) System.Math.Floor((float) rowIndex / PageSize);
            return GetPage(pageIndex);
        }

        private int pageSize;
        private void resetPages () { PageCount = (int) System.Math.Ceiling((float) RowCount / PageSize); }
    }

    /// <summary>Paginates queries of an NHibernate session (either HQL or SQL).</summary>
    public class NHibernateQueryReader : QueryReader
    {
        /// <summary>Constructs an NHibernateQueryReader given an NHibernate query (either HQL or SQL) and a total row count.</summary>
        public NHibernateQueryReader (NHibernate.IQuery query, int rowCount)
        {
            Query = query;
            RowCount = rowCount;
            PageSize = 1000;
        }

        /// <summary>Gets the NHibernate query (either HQL or SQL).</summary>
        public NHibernate.IQuery Query { get; private set; }

        /// <summary>Gets a QueryPage for the specified page index.</summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public override QueryPage GetPage (int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= PageCount)
                throw new ArgumentOutOfRangeException();

            int firstRowIndex = pageIndex * PageSize;
            return new QueryPage(pageIndex, Query.SetFirstResult(firstRowIndex)
                                                 .SetMaxResults(PageSize)
                                                 .List());
        }
    }

    /// <summary>Paginates queries and keeps the most recently pages cached.</summary>
    public class QueryPageCache : QueryReader
    {
        /// <summary>Constructs a QueryPageCache given an NHibernate query (either HQL or SQL) and a total row count.</summary>
        public QueryPageCache (NHibernate.IQuery query, int rowCount)
        {
            reader = new NHibernateQueryReader(query, rowCount);
            cacheByPageIndex = new MruDictionary<int, QueryPage>(2);
            PageSize = 1000;
        }

        /// <summary>
        /// Gets a QueryPage for the specified page index.
        /// If the page is already cached, it returns the cached page,
        /// otherwise a new copy of the page is retrieved and cached.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public override QueryPage GetPage (int pageIndex)
        {
            QueryPage result;
            cacheByPageIndex.TryGetValue(pageIndex, out result);
            if (result == null)
            {
                result = reader.GetPage(pageIndex);
                cacheByPageIndex.Add(pageIndex, result);
            }
            return result;
        }

        /// <summary>
        /// Gets a QueryPage for the specified row index.
        /// If the page is already cached, it returns the cached page,
        /// otherwise a new copy of the page is retrieved and cached.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public override QueryPage GetPageForRow (int rowIndex)
        {
            return base.GetPageForRow(rowIndex);
        }

        /// <summary>Gets or sets the maximum number of pages the cache can hold.</summary>
        public int CacheSize
        {
            get { return cacheByPageIndex.MaximumCapacity; }
            set { cacheByPageIndex.SetMaximumCapacity(value); }
        }

        /// <summary>Gets or sets the maximum number of rows in a QueryPage.</summary>
        public new int PageSize
        {
            get { return reader.PageSize; }
            set
            {
                reader.PageSize = value;
                cacheByPageIndex.Clear();
            }
        }

        /// <summary>Gets the number of pages depending on PageSize and RowCount.</summary>
        public new int PageCount { get { return reader.PageCount; } }

        /// <summary>Gets the total number of rows for the query.</summary>
        public new int RowCount { get { return reader.RowCount; } }

        MruDictionary<int, QueryPage> cacheByPageIndex;
        QueryReader reader;
    }
}