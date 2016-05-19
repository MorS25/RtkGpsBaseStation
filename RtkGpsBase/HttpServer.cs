using System;
using System.Diagnostics;
using System.IO;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal sealed class HttpServer : IDisposable
    {
        public delegate void HttpRequestReceivedEvent(HttpRequest request);

        private readonly StreamSocketListener _listener;
        private readonly int _port;

        private readonly SerialPort _serialPort;

        public HttpServer(int serverPort)
        {
            _serialPort = new SerialPort("AI041RYG", 57600, 5000, 5000);

            if (_listener == null)
            {
                _listener = new StreamSocketListener();
            }
            _port = serverPort;

            _listener.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        public event HttpRequestReceivedEvent OnRequestReceived;

        internal async void StartServer()
        {
            try
            {
                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Http Server could not bind to service {0}. Error {1}", _port, ex.Message);
            }
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            HttpRequest request;
            using (var stream = socket.InputStream)
            {
                var parser = new HttpRequestParser();
                request = await parser.GetHttpRequestForStream(stream);
                OnRequestReceived?.Invoke(request);
            }

            using (var output = socket.OutputStream)
            {
                try
                {
                    if (request.Method == "GET")
                    {
                        WriteResponse(output);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private void WriteResponse(IOutputStream output)
        {
            var resp = output.AsStreamForWrite();

            while (true) //Stream whatever we get from the base station GPS to the client
            {
                try
                {
                    var bodyArray = _serialPort.Read();

                    var stream = new MemoryStream(bodyArray);
                    resp.WriteAsync(bodyArray, 0, bodyArray.Length);
                    stream.CopyToAsync(resp);
                    resp.FlushAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                    break;
                }
            }
        }
    }
}