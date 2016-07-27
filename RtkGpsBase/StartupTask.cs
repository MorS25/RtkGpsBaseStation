using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

#pragma warning disable 4014
namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        private readonly HttpServer _httpServer = new HttpServer();
        private readonly SparkFunSerial16X2Lcd _display = new SparkFunSerial16X2Lcd();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            await _display.InitializeAsync();
            
            var httpServer = new HttpServer();
            await httpServer.Start(_display);
        }
    }
}