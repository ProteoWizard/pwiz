using System;
using System.Collections.Generic;

namespace pwiz.Common.SystemUtil.Caching
{
    public class WorkOrder
    {
        private int _hashCode;
        public WorkOrder(Producer calculator, object parameter)
        {
            Calculator = calculator;
            Parameter = parameter;
            unchecked
            {
                _hashCode = Calculator.GetHashCode() * 397 ^ ValueTuple.Create(Parameter).GetHashCode();
            }
        }

        public Producer Calculator { get; }
        public object Parameter { get; private set; }
        public Type ValueType
        {
            get { return Calculator.ValueType; }
        }

        protected bool Equals(WorkOrder other)
        {
            return Equals(Calculator, other.Calculator) && Equals(Parameter, other.Parameter);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((WorkOrder)obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public IEnumerable<WorkOrder> GetInputs()
        {
            return Calculator.GetInputs(Parameter);
        }
    }
}
