using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.Common.Chemistry
{
    public interface IMassDistribution<T> where T:IMassDistribution<T>
    {
        public T Add(T rhs);
        public T Multiply(int factor);
    }
}
