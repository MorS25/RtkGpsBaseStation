using System.Diagnostics;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;

namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask{
        private static BackgroundTaskDeferral _deferral;
        private HttpServer _httpServer;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            //var gpioController = GpioController.GetDefault();
            //var pin = gpioController.OpenPin(21); //Just using leg six
            //pin.SetDriveMode(GpioPinDriveMode.Output); //Will this power an LED, as well as trigger the event?
            ////pin.ValueChanged += Pin_ValueChanged;
            //pin.Write(GpioPinValue.High);
            //pin.Write(GpioPinValue.Low);

            _httpServer = new HttpServer(8181);
            _httpServer.StartServer();
        }

        private void Pin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            Debug.WriteLine("Edge " + args.Edge);
        }
    }
}