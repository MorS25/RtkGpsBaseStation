using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace RtkGpsBase{
    internal class SerialPort
    {
        internal SerialDevice SerialDevice;
        private DataReader _dataReader;

        internal SerialPort(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            Task.Factory.StartNew(async () =>
            {
                while (SerialDevice == null)
                {
                    var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                    var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier)); //Onboard is "UART0"

                    if (selectedPort == null)
                    {
                        Debug.WriteLine($"Could not find device information for {identifier}. Retrying in 2 seconds... ");

                        await Task.Delay(2000);
                        continue;
                    }

                    SerialDevice = await SerialDevice.FromIdAsync(selectedPort.Id);

                    if (SerialDevice == null)
                    {
                        Debug.WriteLine($"Could not open serial port at {identifier}. Usually an app manifest issue. Retrying in 2 seconds...  ");

                        await Task.Delay(2000);
                        continue;
                    }

                    Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

                    SerialDevice.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                    SerialDevice.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                    SerialDevice.BaudRate = (uint)baudRate;
                    SerialDevice.Parity = SerialParity.None;
                    SerialDevice.StopBits = SerialStopBitCount.One;
                    SerialDevice.DataBits = 8;
                    SerialDevice.Handshake = SerialHandshake.None;

                    _dataReader = new DataReader(SerialDevice.InputStream) {InputStreamOptions = InputStreamOptions.Partial};
                }
            });
        }

        internal void Close()
        {
            SerialDevice?.Dispose();
            SerialDevice = null;
        }

        internal byte[] Read()
        {
            var buffer = new byte[512];

            try
            {
                var bytesRead = _dataReader.LoadAsync(512).AsTask().Result;

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