using SkylineTool;

namespace ToolServiceTestHarness
{
    public class ArgumentConverter
    {
        public object ConvertArgument(string value, Type type)
        {
            if (type == typeof(string))
            {
                return value;
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var elements = ConvertList(value, elementType).ToList();
                var array = Array.CreateInstance(elementType, elements.Count);
                for (int i = 0; i < elements.Count; i++)
                {
                    array.SetValue(elements[i], i);
                }

                return array;
            }

#pragma warning disable CS0612
            if (type == typeof(DocumentLocation))
            {
                return DocumentLocation.Parse(value);
            }
#pragma warning restore CS0612

            throw new ArgumentException(string.Format(@"Unsupported type {0}", type), nameof(type));
        }

        public IEnumerable<object> ConvertList(string value, Type elementType)
        {
            int lineNumber = 0;
            using var stringReader = new StringReader(value);
            while (true)
            {
                string? line = stringReader.ReadLine();
                if (line == null)
                {
                    yield break;
                }
                lineNumber++;
                object elementValue;
                try
                {
                    elementValue = ConvertArgument(line, elementType);
                }
                catch (Exception ex)
                {
                    throw new LineNumberException(ex, lineNumber);
                }

                yield return elementValue;
            }
        }
    }
}
