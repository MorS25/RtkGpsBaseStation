using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal sealed class HttpServer : IDisposable
    {
        public delegate void HttpRequestReceivedEvent(HttpRequest request);
        public event HttpRequestReceivedEvent OnRequestReceived;

        private static StreamSocketListener _listener;
        private readonly int _port;
        private SerialDevice _rtkGps;
        private DataReader _dataReader;

        public HttpServer(int serverPort)
        {
            if (_listener != null)
                return;

            _listener = new StreamSocketListener();

            _port = serverPort;
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        internal async Task Start()
        {
            _rtkGps = await SerialDeviceHelper.GetSerialDevice("AI041RYG", 57600);
            await Task.Delay(500);
            if (_rtkGps == null)
                return;

            _dataReader = new DataReader(_rtkGps.InputStream) {InputStreamOptions =  InputStreamOptions.Partial};

            try
            {
                _listener.ConnectionReceived += (s, e) => { Task.Factory.StartNew(async () => await ProcessRequestAsync(e.Socket)); };

                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception)
            {
                await Display.Write($"failed {_port}");
            }
        }

        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            await Display.Write($"{socket.Information.RemoteAddress}");

            using (var stream = socket.InputStream)
            {
                var parser = new HttpRequestParser();
                var request = await parser.GetHttpRequestForStream(stream);
                if (request.Method != "GET")
                    return;
            }

            try
            {
                using (var output = socket.OutputStream)
                {
                    using (var resp = output.AsStreamForWrite())
                    {
                        while (true) //Stream whatever we get from the base station GPS to the client
                        {
                            var bytesIn = await _dataReader.LoadAsync(512);

                            if (bytesIn == 0)
                                continue;

                            var buffer = new byte[bytesIn];
                            _dataReader.ReadBytes(buffer);

                            await resp.WriteAsync(buffer, 0, buffer.Length);
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