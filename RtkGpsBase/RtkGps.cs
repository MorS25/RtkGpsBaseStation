using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal class RtkGps
    {
        private SerialDevice _serialDevice;
        private DataReader _dataReader;


        /// <summary>
        /// This has only been tested with NS-HP series of GPS
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="baudRate"></param>
        /// <param name="readTimeoutMs"></param>
        /// <param name="writeTimeoutMs"></param>
        internal RtkGps(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            IncomingNtripData = new List<byte[]>();

            Task.Factory.StartNew(async () =>
            {
                while (_serialDevice == null)
                {
                    var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                    var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier)); //Onboard is "UART0"

                    if (selectedPort == null)
                    {
                        Debug.WriteLine($"Could not find device information for {identifier}. Retrying in 2 seconds... ");

                        await Task.Delay(2000);
                        continue;
                    }

                    _serialDevice = await SerialDevice.FromIdAsync(selectedPort.Id);

                    if (_serialDevice == null)
                    {
                        Debug.WriteLine($"Could not open serial port at {identifier}. Usually an app manifest issue. Retrying in 2 seconds...  ");

                        await Task.Delay(2000);
                        continue;
                    }

                    Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

                    _serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                    _serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                    _serialDevice.BaudRate = (uint)baudRate;
                    _serialDevice.Parity = SerialParity.None;
                    _serialDevice.StopBits = SerialStopBitCount.One;
                    _serialDevice.DataBits = 8;
                    _serialDevice.Handshake = SerialHandshake.None;

                    _dataReader = new DataReader(_serialDevice.InputStream) {InputStreamOptions = InputStreamOptions.Partial};
                }
            });
        }

        internal List<byte[]> IncomingNtripData { get; }

        internal async void Start()
        {
            await Task.Factory.StartNew(async() =>
            {
                while (true)
                {
                    if (_serialDevice == null)
                    {
                        Task.Delay(100).Wait();
                        continue;
                    }

                    if (IncomingNtripData.Count > 8)
                        IncomingNtripData.RemoveAt(0);

                    var r = await Read();

                    if (r.Length > 1)
                        IncomingNtripData.Add(r);
                }
                // ReSharper disable once FunctionNeverReturns
            });
        }

        private async Task<byte[]> Read()
        {
            var buffer = new byte[128];

            try
            {
                var bytesRead = await _dataReader.LoadAsync(512);

                if (bytesRead > 0)
                {
                    buffer = new byte[bytesRead];
                    _dataReader.ReadBytes(buffer);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);

                return new byte[1];
            }

            return buffer;
        }
    }
}