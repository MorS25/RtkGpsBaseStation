using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

#pragma warning disable 4014
namespace RtkGpsBase
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static BackgroundTaskDeferral _deferral;

        private NtripServer _ntripServer;
        private IoTClient _ioTClient;
        private readonly SparkFunSerial16X2Lcd _display = new SparkFunSerial16X2Lcd();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            await _display.InitializeAsync();

            _ioTClient = new IoTClient(_display);
            _ntripServer = new NtripServer(_display, _ioTClient);

            await _ioTClient.InitializeAsync();
            await _ntripServer.InitializeAsync();

            await Task.WhenAll(new Task[] {_ntripServer.StartAsync(), _ioTClient.StartAsync()});
        }
    }
}