using Windows.ApplicationModel.Background;

namespace RtkGpsBase
{
    //The http server code was taken from a windows 10 IoT Core example
    public sealed class StartupTask : IBackgroundTask{
        private static BackgroundTaskDeferral _deferral;
        private HttpServer _httpServer;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            _httpServer = new HttpServer(8181);
            _httpServer.StartServer();
        }
    }
}