using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Threading;

using MetroLog;
using MetroLog.Targets;

namespace NewTelegramBot
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private TelegramBotClient _telebot;
        private CancellationTokenSource _ctSrc;
        private CancellationToken _ct;
        private ThreadPoolTimer _CheckServerStateTimer;
        private bool _serverAwake;
        private long _ChatID;
        private ResourceLoader _conf;
        private Helpers.RpcAria2Helper _aria2;
        private TelegramBotState _state;
        private ILoggerAsync _log;
        public ResourceLoader Config
        {
            get { return _conf; }
        }

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            taskInstance.Canceled += TaskInstance_Canceled;
            Initialize(taskInstance.GetDeferral());
            int offset = 0;
            List<Task> taskList = new List<Task>();
            await SendMessageAsync("Bot gestartet.");
            while (! _ct.IsCancellationRequested)
            {
                Update[] updates = await _telebot.GetUpdatesAsync(offset);
                if (updates.Any())
                {
                    offset = updates.Max(u => u.Id) + 1;
                    foreach (Update update in updates)
                    {
                        Message message = update.Message;
                        if (message == null)
                        {
                            message = update.CallbackQuery.Message;
                        }

                        if (message.Chat.Id == _ChatID)
                        {
                            taskList.Add(_telebot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing, _ct));
                            if (update.Type == UpdateType.MessageUpdate)
                            {
                                string msg = string.Empty;
                                if (! string.IsNullOrEmpty(message.Text))
                                {
                                    if (message.Text.Equals("\ud83d\udd5a Server starten"))
                                    {
                                        _serverAwake = await NetworkHelper.IsServerAwake();
                                        msg = "Der Server ist bereits an.";
                                        if (!_serverAwake)
                                        {
                                            taskList.Add(NetworkHelper.WakeTheServer().AsTask());
                                            ThreadPoolTimer.CreateTimer(CheckServerStateTimerElapsedHandler, new TimeSpan(0, 2, 0));
                                            msg = "Der Server wird gestartet";
                                        }
                                    }
                                    else if (message.Text.Equals("\ud83d\udd17 Download Modus"))
                                    {
                                        msg = "Download Modus aktiv";
                                        _state = TelegramBotState.DownloadMode;
                                    }
                                    else if (message.Text.Equals("\U0001f519 Download Modus beenden"))
                                    {
                                        msg = "Download Modus beendet";
                                        _state = TelegramBotState.MainMenu;
                                    }
                                }
                                if (string.IsNullOrEmpty(msg))
                                {
                                    if (_state >= TelegramBotState.DownloadMode && _state <= TelegramBotState.DownloadQued)
                                    {
                                        taskList.Add(_aria2.DownloadUri(message.Text, message.MessageId));
                                        msg = "Download eingereiht";
                                        _state = TelegramBotState.DownloadQued;
                                    }
                                    else
                                    {
                                        msg = "Kommando nicht verstanden.";
                                    }
                                }
                                taskList.Add(SendMessageAsync(msg).AsTask<int>());
                            }
                            else if (update.Type == UpdateType.CallbackQueryUpdate)
                            {
                                taskList.Add(SendMessageAsync("Callback Stuff", message.MessageId).AsTask<int>());
                                //ToDo: Mit dem Download interagieren
                            }
                        }
                    }
                    Task.WaitAll(taskList.ToArray(), _ct);
                }
                await Task.Delay(new TimeSpan(0, 0, 10), _ct);
            }
            _deferral.Complete();
        }

        public IAsyncOperation<int> SendMessageAsync(string message)
        {
            return SendMessageAsync(message, 0);
        }

        public IAsyncOperation<int> SendMessageAsync(string message, int replyToMessageId)
        {
            return SendMessage(message, replyToMessageId).AsAsyncOperation<int>();
        }

        private async Task<int> SendMessage(string message, int replyToMessageId)
        {
            await _log.TraceAsync($"Senden der Nachricht '{message}' mit Bezug zur Nachricht {replyToMessageId}").ConfigureAwait(false);
            Message msg = await _telebot.SendTextMessageAsync(_ChatID, message, replyToMessageId: replyToMessageId, replyMarkup: Helpers.KeyBoard.GetKeyboardByTelegramBotState(_state),
                cancellationToken: _ct);
            return msg.MessageId;
        }

        public IAsyncAction EditMessageAsync(string message, int editMessageId)
        {
            _log.TraceAsync($"Editieren der Nachricht '{message}' mit der ID {editMessageId}");
            return _telebot.EditMessageTextAsync(_ChatID, editMessageId, message, cancellationToken: _ct).AsAsyncAction();
        }

        private void Initialize(BackgroundTaskDeferral deferral)
        {
            LoggingConfiguration logConf = new LoggingConfiguration();
#if DEBUG
            logConf.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
            logConf.AddTarget(LogLevel.Debug, LogLevel.Fatal, new DebugTarget());
#else
            logConf.AddTarget(LogLevel.Error, LogLevel.Fatal, new StreamingFileTarget());
#endif
            logConf.IsEnabled = true;
            LogManagerFactory.DefaultConfiguration = logConf;
            _log = (ILoggerAsync)LogManagerFactory.DefaultLogManager.GetLogger<StartupTask>();
            _deferral = deferral;
            _ctSrc = new CancellationTokenSource();
            _ct = _ctSrc.Token;
            _conf = ResourceLoader.GetForViewIndependentUse("Config");
            _CheckServerStateTimer = ThreadPoolTimer.CreatePeriodicTimer(CheckServerStateTimerElapsedHandler, new TimeSpan(0, 5, 0));
            _telebot = new TelegramBotClient(_conf.GetString("TelegramToken"));
            _ChatID = Convert.ToInt64(_conf.GetString("TelegramChatID"));
            _aria2 = new Helpers.RpcAria2Helper(this);
            _serverAwake = false;
            _state = TelegramBotState.None;
            _log.TraceAsync("Initialize abgeschlossen.");
        }

        private async void CheckServerStateTimerElapsedHandler(ThreadPoolTimer timer)
        {
            await _log.InfoAsync("Timer abgelaufen.");
            bool serverAwake = await NetworkHelper.IsServerAwake();
            if (serverAwake != _serverAwake)
            {
                _serverAwake = serverAwake;
                await _log.TraceAsync("Toggle Server State via Timer");
                if (timer.Delay == null)
                {
                    string serverState = serverAwake ? "eingeschaltet" : "ausgeschlatet";
                    string message = $"Der Server ist nun {serverState}! {Environment.NewLine} Erkannt durch Timer um {timer.Period.ToString()}";
                    await SendMessageAsync(message);
                }
            }
        }

        private async void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            await SendMessageAsync("Bot geht aus.");
            await _log.InfoAsync($"Task wurde beendet, weil: {reason.ToString()}");
            _CheckServerStateTimer.Cancel();
            _ctSrc.Cancel();
            _ctSrc.Dispose();
            _deferral.Complete();
        }
    }
}
