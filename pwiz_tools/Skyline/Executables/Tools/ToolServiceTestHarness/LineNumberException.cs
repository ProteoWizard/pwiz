namespace ToolServiceTestHarness
{
    public class LineNumberException : Exception
    {
        public LineNumberException(Exception cause, int lineNumber) : base(cause.Message, cause)
        {
            LineNumber = lineNumber;
        }

        public int LineNumber { get; }
    }
}
