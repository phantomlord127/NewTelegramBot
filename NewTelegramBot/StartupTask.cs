using Windows.ApplicationModel.Background;

namespace NewTelegramBot
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private TelegramBot _bot;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.Canceled += TaskInstance_Canceled;
            _deferral = taskInstance.GetDeferral();
            _bot = new TelegramBot();
            _bot.Start();
        }

        private async void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            await _bot.Close(reason);
            _deferral.Complete();
        }

    }
}
