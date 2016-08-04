using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;


namespace RtkGpsBase
{
    /// <summary>
    /// Azure IoT Hub MQTT client
    /// </summary>
    internal sealed class IoTClient
    {
        private DeviceClient _deviceClient;
        private readonly SparkFunSerial16X2Lcd _display;

        internal static event EventHandler<IotEventArgs> IotEvent;

        private string _connectionString =
            @"";

        internal IoTClient(SparkFunSerial16X2Lcd display)
        {
            _display = display;
        }

        internal async Task InitializeAsync()
        {
            try
            {
                _deviceClient = DeviceClient.CreateFromConnectionString(_connectionString, TransportType.Amqp); //add connection string

                await _deviceClient.OpenAsync();
            }
            catch (Exception)
            {
                await _display.WriteAsync("IoT failed");
            }
        }

        internal async Task SendEventAsync(string eventData)
        {
            if (_deviceClient == null || string.IsNullOrEmpty(eventData))
                return;

            try
            {
                var eventMessage = new Message(Encoding.UTF8.GetBytes(eventData));

                await _deviceClient.SendEventAsync(eventMessage);
            }
            catch (Exception)
            {
                await _display.WriteAsync("Send failed");
            }
        }

        internal async Task SendEventAsync(byte[] eventData)
        {
            try
            {
                var eventMessage = new Message(eventData);

                await _deviceClient.SendEventAsync(eventMessage);
            }
            catch (Exception)
            {
                await _display.WriteAsync("Send failed");
            }
        }

        /// <summary>
        /// This starts waiting for messages from the IoT Hub. 
        /// </summary>
        /// <returns></returns>
        internal async Task StartAsync()
        {
            if (_deviceClient == null)
                return;

            while (true)
            {
                try
                {
                    var receivedMessage = await _deviceClient.ReceiveAsync(new TimeSpan(int.MaxValue));

                    if (receivedMessage == null)
                        continue;
                    
                    foreach (var prop in receivedMessage.Properties)
                    {
                        await _display.WriteAsync($"{prop.Key} - {prop.Value}");
                    }

                    await _deviceClient.CompleteAsync(receivedMessage);

                    var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());

                    IotEvent?.Invoke(null, new IotEventArgs { EventData = receivedMessage.Properties, MessageData = messageData });
                }
                catch
                {
                    await _display.WriteAsync("Receive failed");
                    await Task.Delay(10000);
                }
            }
        }
    }

    internal class IotEventArgs : EventArgs
    {
        internal IDictionary<string, string> EventData { get; set; }

        internal string MessageData { get; set; }
    }
}