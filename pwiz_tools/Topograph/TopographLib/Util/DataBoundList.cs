using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NHibernate;

namespace turnover.Util
{
    //public class DataBoundList<T> : List<T>
    //{
    //    private ISession session;
    //    private List<T> list;
    //    public DataBoundList(ISession session, IList<T> list)
    //    {
    //        this.session = session;
    //        this.list = new List<T>(list);
    //    }

    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return GetEnumerator();
    //    }

    //    public IEnumerator<T> GetEnumerator()
    //    {
    //        return list.GetEnumerator();
    //    }

    //    public void Add(T item)
    //    {
    //        ITransaction transaction = session.BeginTransaction();
    //        session.Save(item);
    //        transaction.Commit();
    //        list.Add(item);
    //    }

    //    public void Clear()
    //    {
    //        throw new System.NotImplementedException();
    //    }

    //    public bool Contains(T item)
    //    {
    //        return list.Contains(item);
    //    }

    //    public void CopyTo(T[] array, int arrayIndex)
    //    {
    //        throw new System.NotImplementedException();
    //    }

    //    public bool Remove(T item)
    //    {
    //        ITransaction transaction = session.BeginTransaction();
    //        session.Delete(item);
    //        transaction.Commit();
    //        return list.Remove(item);
    //    }

    //    public int Count
    //    {
    //        get { return list.Count; }
    //    }

    //    public bool IsReadOnly
    //    {
    //        get { return false; }
    //    }

    //    public int IndexOf(T item)
    //    {
    //        return list.IndexOf(item);
    //    }

    //    public void Insert(int index, T item)
    //    {
    //        ITransaction transaction = session.BeginTransaction();
    //        session.Save(item);
    //        transaction.Commit();
    //        list.Insert(index, item);
    //    }

    //    public void RemoveAt(int index)
    //    {
    //        Remove(list[index]);
    //    }

    //    public T this[int index]
    //    {
    //        get { return list[index]; }
    //        set { throw new System.NotImplementedException(); }
    //    }
    //}
}
