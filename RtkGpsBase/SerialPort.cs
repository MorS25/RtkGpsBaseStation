using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace RtkGpsBase
{
    internal class SerialPort
    {
        private SerialDevice _serialPort;
        private readonly SfSerial16X2Lcd _lcd;

        internal SerialPort(SfSerial16X2Lcd lcd = null)
        {
            _lcd = lcd;
        }

        internal async Task Open(string identifier, int baudRate, int readTimeoutMs, int writeTimeoutMs)
        {
            while (_serialPort == null)
            {
                var deviceInformationCollection = await DeviceInformation.FindAllAsync(SerialDevice.GetDeviceSelector());
                var selectedPort = deviceInformationCollection.FirstOrDefault(d => d.Id.Contains(identifier) || d.Name.Equals(identifier));

                if (selectedPort == null)
                {
                    Debug.WriteLine($"Could not find device information for {identifier}. Retrying in 2 seconds... ");

                    if (_lcd != null)
                    {
                        await _lcd.WriteToFirstLine("Could not find");
                        await _lcd.WriteToSecondLine(identifier);
                    }


                    await Task.Delay(2000);
                    continue;
                }

                _serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);

                if (_serialPort == null)
                {
                    Debug.WriteLine($"Could not open serial port at {identifier}. Usually an app manifest issue. Retrying in 2 seconds...  ");

                    if (_lcd != null)
                    {
                        await _lcd.WriteToFirstLine("Could not open");
                        await _lcd.WriteToSecondLine(identifier);
                    }

                    await Task.Delay(2000);
                    continue;
                }

                Debug.WriteLine($"Found - {identifier} as {selectedPort.Id}");

                if (_lcd != null)
                    await _lcd.Write($"Found - {identifier}");

                _serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readTimeoutMs);
                _serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeoutMs);
                _serialPort.BaudRate = (uint)baudRate;
                _serialPort.Parity = SerialParity.None;
                _serialPort.StopBits = SerialStopBitCount.One;
                _serialPort.DataBits = 8;
                _serialPort.Handshake = SerialHandshake.None;
            }
        }

        internal void Close()
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        internal async Task Write(byte[] data)
        {
            await _serialPort.OutputStream.WriteAsync(data.AsBuffer());
        }

        internal async Task<byte[]> ReadBytes(uint count)
        {
            if (_serialPort == null)
                return new byte[1];

            var buffer = new Buffer(count);

            await _serialPort.InputStream.ReadAsync(buffer, count, InputStreamOptions.Partial).AsTask();

            return buffer.ToArray();
        }
    }
}