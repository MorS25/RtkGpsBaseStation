using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace RtkGpsBase{
    internal sealed class HttpServer : IDisposable
    {
        public delegate void HttpRequestReceivedEvent(HttpRequest request);
        public event HttpRequestReceivedEvent OnRequestReceived;

        private static StreamSocketListener _listener;
        private readonly int _port;
        private readonly SerialPort _rtkGps;
        private readonly SfSerial16X2Lcd _lcd;

        public HttpServer(int serverPort, SfSerial16X2Lcd lcd)
        {
            if (_listener != null)
                return;

            _lcd = lcd;

            _listener = new StreamSocketListener();

            _rtkGps = new SerialPort(lcd);

            _port = serverPort;
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        internal async Task Start()
        {
            await _rtkGps.Open("AI041RYG", 57600, 5000, 5000);

            try
            {
                _listener.ConnectionReceived += (s, e) => { Task.Factory.StartNew(async () => await ProcessRequestAsync(e.Socket)); };

                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception ex)
            {
                await _lcd.WriteToFirstLine("Could not bind");
                await _lcd.WriteToSecondLine($"to {_port}");
                Debug.WriteLine("Http Server could not bind to service {0}. Error {1}", _port, ex.Message);
            }
        }

        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            await _lcd.WriteToFirstLine($"Connection from {socket.Information.RemoteAddress}");
            await _lcd.WriteToSecondLine($"{socket.Information.RemoteAddress}");

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
                            var data = await _rtkGps.ReadBytes(400);

                            await resp.WriteAsync(data, 0, data.Length);
                            await resp.FlushAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                await _lcd.WriteToFirstLine("Disconnected");
                await _lcd.WriteToSecondLine($"{socket.Information.RemoteAddress}");

                Debug.WriteLine(e);
            }
        }
    }
}