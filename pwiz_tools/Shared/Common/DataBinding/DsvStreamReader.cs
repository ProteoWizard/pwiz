using System.Collections.Generic;
using System.IO;

namespace pwiz.Common.DataBinding
{
    public class DsvStreamReader
    {
        private TextReader _reader;
        private char _separator;
        private List<string> _headers;
        private List<string> _currentLineFields;
        private string _currentLine;

        public DsvStreamReader(TextReader reader, char separator)
        {
            _reader = reader;
            if (reader == null || reader.Peek() < 0)
                throw new IOException("Stream is null or empty.");
            _separator = separator;
            _headers = new List<string>();
            var headersLine = _reader.ReadLine();
            if (headersLine == null)
                throw new IOException("Empty stream.");
            _headers = new List<string>(headersLine.Split(_separator));
        }

        public void ReadLine()
        {
            var _currentLine = _reader.ReadLine();
            if (_currentLine == null)
                return;
            _currentLineFields = new List<string>(_currentLine.Split(_separator));
        }

        public string CurrentLine => _currentLine;
        public bool EndOfStream
        {
            get { return _reader.Peek() < 0; }
        }

        public void Close()
        {
            _reader.Close();
        }

        public string this[string header]
        {
            get
            {
                int index = _headers.IndexOf(header);
                if (index == -1)
                    throw new IOException(string.Format("Header {0} not found in DSV file.", header));
                return _currentLineFields[index];
            }
        }
        public bool HasHeader(string header)
        {
            return _headers.Contains(header);
        }

        public bool TryGetColumn(string header, out string value)
        {
            int index = _headers.IndexOf(header);
            if (index == -1)
            {
                value = null;
                return false;
            }
            value = _currentLineFields[index];
            return true;
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
            }
        }
    }
}