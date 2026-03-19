using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SkylineTool;

namespace SortProteins
{
    /// <summary>
    /// Simple JSON pipe client for communicating with Skyline's JsonToolServer.
    /// Derives the JSON pipe name from the legacy connection name passed via --connection_name.
    /// </summary>
    public class JsonClient(string connectionName) : IDisposable
    {
        private readonly string _pipeName = JsonToolConstants.GetJsonPipeName(connectionName);
        private NamedPipeClientStream? _pipe;

        public string Call(string method, params string[] args)
        {
            EnsureConnected();
            var request = new { method, args };
            byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
            _pipe!.Write(requestBytes, 0, requestBytes.Length);
            _pipe.Flush();
            _pipe.WaitForPipeDrain();

            byte[] responseBytes = ReadAllBytes(_pipe);
            string responseJson = Encoding.UTF8.GetString(responseBytes);

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                string? error = errorElement.GetString();
                throw new InvalidOperationException(error ?? "Unknown error from Skyline");
            }

            if (root.TryGetProperty("result", out var resultElement))
            {
                if (resultElement.ValueKind == JsonValueKind.Null)
                    return null!;
                if (resultElement.ValueKind == JsonValueKind.Number)
                    return resultElement.GetRawText();
                if (resultElement.ValueKind == JsonValueKind.Object ||
                    resultElement.ValueKind == JsonValueKind.Array)
                    return resultElement.GetRawText();
                return resultElement.GetString()!;
            }

            return null!;
        }

        private void EnsureConnected()
        {
            if (_pipe is { IsConnected: true })
                return;
            _pipe?.Dispose();
            _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            _pipe.Connect(5000);
            _pipe.ReadMode = PipeTransmissionMode.Message;
        }

        private static byte[] ReadAllBytes(PipeStream stream)
        {
            var memoryStream = new MemoryStream();
            do
            {
                var buffer = new byte[65536];
                int count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0)
                    return memoryStream.ToArray();
                memoryStream.Write(buffer, 0, count);
            } while (!stream.IsMessageComplete);
            return memoryStream.ToArray();
        }

        public void Dispose()
        {
            _pipe?.Dispose();
        }
    }
}
