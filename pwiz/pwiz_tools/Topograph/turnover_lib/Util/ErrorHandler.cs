using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Util
{
    public static class ErrorHandler
    {
        public const int MaxErrorCount = 1000;
        static readonly List<Error> errors = new List<Error>();
        public static void AddError(Error error)
        {
            lock(errors)
            {
                while (errors.Count >= MaxErrorCount)
                {
                    errors.RemoveAt(0);
                }
                errors.Add(error);
            }
            var handler = ErrorAdded;
            if (handler != null)
            {
                handler.Invoke(error);
            }
        }

        public static void LogException(String component, String message, Exception exception)
        {
            if (exception is LockException)
            {
                return;
            }
            AddError(new Error(component, message, exception));
            Console.Out.WriteLine(exception);
        }

        public static void ClearErrors()
        {
            lock(errors)
            {
                errors.Clear();
            }
        }

        public static IList<Error> GetErrors()
        {
            lock(errors)
            {
                return errors.ToArray();
            }
        }

        public delegate void ErrorAddedHandler(Error error);

        public static event ErrorAddedHandler ErrorAdded;
    }
}
