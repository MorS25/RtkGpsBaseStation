using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace RtkGpsBase{
    internal sealed class HttpServer : IDisposable
    {
        public delegate void HttpRequestReceivedEvent(HttpRequest request);
        public event HttpRequestReceivedEvent OnRequestReceived;

        private static StreamSocketListener _listener;
        private readonly int _port;

        private readonly RtkGps _rtkGpsData;

        public HttpServer(int serverPort)
        {
            if (_listener != null)
                return;

            _listener = new StreamSocketListener();

            _rtkGpsData = new RtkGps("AI041RYG", 57600, 5000, 5000);

            _port = serverPort;

            _rtkGpsData.Start();
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        internal async Task Start()
        {
            try
            {
                _listener.ConnectionReceived += (s, e) => { Task.Factory.StartNew(async () => await ProcessRequestAsync(e.Socket)); };

                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Http Server could not bind to service {0}. Error {1}", _port, ex.Message);
            }
        }

        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            Debug.WriteLine($"Connection from {socket.Information.RemoteAddress}, {socket.Information.RemoteHostName}");

            HttpRequest request;
            using (var stream = socket.InputStream)
            {
                var parser = new HttpRequestParser();
                request = await parser.GetHttpRequestForStream(stream);
                OnRequestReceived?.Invoke(request);
            }

            try
            {
                using (var output = socket.OutputStream)
                {
                    if (request.Method != "GET") return;

                    using (var resp = output.AsStreamForWrite())
                    {
                        while (true) //Stream whatever we get from the base station GPS to the client
                        {
                            if (_rtkGpsData.IncomingNtripData.Count < 1)
                                return;

                            var data = _rtkGpsData.IncomingNtripData[0];
                            var stream = new MemoryStream(data);
                            await resp.WriteAsync(data, 0, data.Length);
                            await stream.CopyToAsync(resp);
                            await resp.FlushAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}