using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            Task.Factory.StartNew(async () =>
            {
                var httpServer = new HttpServer(8000);
                await httpServer.Start();
            }, TaskCreationOptions.LongRunning);
        }
    }
}