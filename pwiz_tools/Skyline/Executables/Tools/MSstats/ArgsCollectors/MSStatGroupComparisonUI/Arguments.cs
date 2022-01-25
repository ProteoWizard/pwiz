using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MSStatArgsCollector
{
    public class Arguments
    {
        private Dictionary<Arg, string> _arguments;

        public Arguments() : this(new Dictionary<Arg, string>())
        {

        }

        private Arguments(Dictionary<Arg, string> dictionary)
        {
            _arguments = dictionary;
        }

        public void Set(Arg key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _arguments.Remove(key);
            }
            else
            {
                _arguments[key] = value;
            }
        }

        public void Set(Arg key, int value)
        {
            _arguments[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(Arg key, double value)
        {
            _arguments[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public string Get(Arg name)
        {
            _arguments.TryGetValue(name, out var value);
            return value;
        }

        public int? GetInt(Arg name)
        {
            string value = Get(name);
            if (value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }
            return null;
        }

        public double? GetDouble(Arg name)
        {
            string value = Get(name);
            if (value != null &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return null;
        }

        public IEnumerable<string> ToArgumentList()
        {
            foreach (var entry in _arguments.OrderBy(kvp => kvp.Key))
            {
                yield return "--" + entry.Key;
                yield return entry.Value;
            }
        }

        public static Arguments FromArgumentList(IList<string> arguments)
        {
            var dictionary = new Dictionary<Arg, string>();
            if (arguments != null)
            {
                for (int i = 0; i < arguments.Count - 1; i++)
                {
                    string key = arguments[i];
                    if (!key.StartsWith("--") || !Enum.TryParse(key.Substring(2), out Arg arg))
                    {
                        continue;
                    }

                    string value = arguments[++i];
                    dictionary[arg] = value;
                }
            }

            return new Arguments(dictionary);
        }
    }
}
