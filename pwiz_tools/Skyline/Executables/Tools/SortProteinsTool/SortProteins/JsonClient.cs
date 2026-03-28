using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace SortProteins
{
    /// <summary>
    /// JSON-RPC 2.0 pipe client for communicating with Skyline's JsonToolServer.
    /// </summary>
    public class JsonClient(string connectionName) : IDisposable
    {
        private const string JSON_PIPE_PREFIX = "SkylineMcpJson-";
        private readonly string _pipeName = JSON_PIPE_PREFIX + connectionName.Replace("-", string.Empty);
        private NamedPipeClientStream? _pipe;

        private static readonly JsonSerializerOptions _snakeCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        public string? Call(string method, params object[] args)
        {
            EnsureConnected();
            var request = new { jsonrpc = "2.0", method, @params = args, id = 1 };
            byte[] requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, _snakeCaseOptions));
            _pipe!.Write(requestBytes, 0, requestBytes.Length);
            _pipe.Flush();
            _pipe.WaitForPipeDrain();

            byte[] responseBytes = ReadAllBytes(_pipe);
            string responseJson = Encoding.UTF8.GetString(responseBytes);

            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorElement))
            {
                string? message = errorElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString()
                    : "Unknown error from Skyline";
                throw new InvalidOperationException(message);
            }

            if (root.TryGetProperty("result", out var resultElement))
            {
                if (resultElement.ValueKind == JsonValueKind.Null)
                    return null;
                if (resultElement.ValueKind == JsonValueKind.Number ||
                    resultElement.ValueKind == JsonValueKind.Object ||
                    resultElement.ValueKind == JsonValueKind.Array)
                    return resultElement.GetRawText();
                return resultElement.GetString();
            }

            return null;
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
