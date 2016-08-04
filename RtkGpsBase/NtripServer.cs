using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal sealed class NtripServer : IDisposable
    {
        private static StreamSocketListener _listener;
        private readonly int _port = 8000;
        private SerialDevice _rtkGps;
        private DataReader _dataReader;
        private readonly SparkFunSerial16X2Lcd _display;
        private static IoTClient _ioTClient;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static byte[] _buffer;
        private Timer _timer = new Timer(TimerCallback, null, 0, 250);
        internal ManualResetEventSlim ManualResetEventSlim = new ManualResetEventSlim(false);
        
        public NtripServer(SparkFunSerial16X2Lcd display, IoTClient ioTClient)
        {
            _display = display;
            _ioTClient = ioTClient;
            _listener = new StreamSocketListener();
        }

        private static async void TimerCallback(object state)
        {
            if (_buffer == null || _ioTClient == null)
                return;

            await _ioTClient.SendEventAsync(_buffer);
        }

        internal async Task InitializeAsync()
        {
            _rtkGps = await SerialDeviceHelper.GetSerialDevice("AI041RYG", 57600, new TimeSpan(0, 0, 0, 0, 12), new TimeSpan(0, 0, 0, 0, 12)); //5hz rate is 12ms

            if (_rtkGps != null)
                _dataReader = new DataReader(_rtkGps.InputStream) {InputStreamOptions = InputStreamOptions.Partial}; //send whatever we 
            else
            {
                await _display.WriteAsync("GPS not found");
                return;
            }

            try
            {
                _listener.ConnectionReceived += async (s, e) => { await ProcessRequestAsync(e.Socket, _cancellationTokenSource.Token); };

                await _listener.BindServiceNameAsync(_port.ToString());
            }
            catch (Exception)
            {
                await _display.WriteAsync($"failed {_port}");
            }
        }

        public void Dispose()
        {
            _listener?.Dispose();
        }

        internal async Task StartAsync()
        {
            while (true) //Read GPS forever
            {
                if (_rtkGps == null || _dataReader == null)
                    continue;

                try
                {
                    var bytesIn = await _dataReader.LoadAsync(512).AsTask();

                    if (bytesIn == 0)
                        continue;

                    _buffer = new byte[bytesIn];
                    _dataReader.ReadBytes(_buffer);
                }
                catch
                {
                    //    
                }

                ManualResetEventSlim.Set();//Send data to clients
                ManualResetEventSlim.Reset();
            }
        }

        private async Task ProcessRequestAsync(StreamSocket socket, CancellationToken token)
        {
            await _display.WriteAsync($"{socket.Information.RemoteAddress} connected");

            try
            {
                using (var output = socket.OutputStream)
                {
                    using (var resp = output.AsStreamForWrite())
                    {
                        while (true) //Stream whatever we get from the base station GPS to the client
                        {
                            ManualResetEventSlim.Wait(token);

                            await resp.WriteAsync(_buffer, 0, _buffer.Length, token);
                            await resp.FlushAsync(token);
                        }
                    }
                }
            }
            catch
            {
                await _display.WriteAsync($"{socket.Information.RemoteAddress} disconnected");
            }
        }
    }
}