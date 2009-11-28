using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pwiz.Topograph.MsData
{
    public static class ErrorLog
    {
        private const int MaxErrors = 1000;
        private static readonly LinkedList<Error> _errors = new LinkedList<Error>();
        public static void LogException(Exception exception)
        {
            Console.Out.WriteLine(exception);
            AddError(new Error(exception));
        }

        public static void AddError(Error error)
        {
            lock(_errors)
            {
                _errors.AddLast(error);
                if (_errors.Count > MaxErrors)
                {
                    _errors.RemoveFirst();
                }
            }
        }

        public static IList<Error> GetErrors()
        {
            lock(_errors)
            {
                return _errors.ToArray();
            }
        }
    }

    public class Error
    {
        public Error(Exception exception)
        {
            Time = DateTime.Now;
            Message = exception.Message;
            Exception = exception;
        }
        public DateTime Time { get; private set;}
        public String Message { get; private set;}
        public Exception Exception { get; private set;}
    }
}
