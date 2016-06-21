using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

#pragma warning disable 4014
namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        private Display _display = new Display();

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            _display.Start();

            var httpServer = new HttpServer(8000);
            httpServer.Start();
        }
    }
}