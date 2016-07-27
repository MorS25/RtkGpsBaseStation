using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal sealed class HttpServer : IDisposable
    {
        private static StreamSocketListener _listener;
        private readonly int _port = 8000;
        private SerialDevice _rtkGps;
        private DataReader _dataReader;

        private SparkFunSerial16X2Lcd _display;

        private readonly CancellationToken _cancellationToken = new CancellationToken(false);

        public HttpServer()
        {
            if (_listener != null)
                return;
            
            _listener = new StreamSocketListener();
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        internal async Task Start(SparkFunSerial16X2Lcd display)
        {
            _display = display;

            _rtkGps = await SerialDeviceHelper.GetSerialDevice("AI041RYG", 57600);

            if (_rtkGps == null)
                return;

            _dataReader = new DataReader(_rtkGps.InputStream) {InputStreamOptions =  InputStreamOptions.Partial};

            try
            {
                _listener.ConnectionReceived += async (s, e) => { await ProcessRequestAsync(e.Socket); };

                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception)
            {
                await _display.WriteAsync($"failed {_port}");
            }
        }

        private async Task ProcessRequestAsync(StreamSocket socket)
        {
            await _display.WriteAsync($"{socket.Information.RemoteAddress}");

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
                            var bytesIn = await _dataReader.LoadAsync(256).AsTask(_cancellationToken);

                            if (bytesIn == 0)
                                continue;

                            Debug.WriteLine(bytesIn);

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