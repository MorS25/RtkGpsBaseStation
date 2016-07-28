using Windows.ApplicationModel.Background;

#pragma warning disable 4014
namespace RtkGpsBase
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        private NtripServer _ntripServer;
        private readonly SparkFunSerial16X2Lcd _display = new SparkFunSerial16X2Lcd();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            await _display.InitializeAsync();
            
            _ntripServer = new NtripServer(_display);
            await _ntripServer.InitializeAsync();
            await _ntripServer.StartAsync();
        }
    }
}