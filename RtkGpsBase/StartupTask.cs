using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            var lcd = new SfSerial16X2Lcd();
            await lcd.Start();

            await lcd.Write("Starting HTTP");

            var httpServer = new HttpServer(8000, lcd);
            await httpServer.Start();
        }
    }
}