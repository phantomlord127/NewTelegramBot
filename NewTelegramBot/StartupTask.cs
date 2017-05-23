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
                            await _telebot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing, _ct).ConfigureAwait(false);
                            if (update.Type == UpdateType.MessageUpdate)
                            {
                                string msg = string.Empty;
                                if (! string.IsNullOrEmpty(message.Text))
                                {
                                    if (message.Text.Equals("\U0001f5a5 Server starten"))
                                    {
                                        _serverAwake = await NetworkHelper.IsServerAwake();
                                        msg = "Der Server ist bereits an.";
                                        if (!_serverAwake)
                                        {
                                            await NetworkHelper.WakeTheServer();
                                            msg = "Der Server wird gestartet";
                                        }
                                    }
                                    else if (message.Text.Equals("\U0001f517 Download Modus"))
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
                                    if (_state == TelegramBotState.DownloadMode)
                                    {
                                        await _aria2.DownloadUri(message.Text, message.MessageId);
                                        msg = "Download eingereiht";
                                    }
                                    else
                                    {
                                        msg = "Kommando nicht verstanden.";
                                    }
                                }
                                await SendMessageAsync(msg);
                            }
                            else if (update.Type == UpdateType.CallbackQueryUpdate)
                            {
                                await SendMessageAsync("Callback Stuff", message.MessageId);
                            }
                        }
                    }
                }
                await Task.Delay(new TimeSpan(0, 0, 10), _ct);
            }

            _deferral.Complete();
        }

        public IAsyncAction SendMessageAsync(string message)
        {
            return SendMessageAsync(message, 0);
        }

        public IAsyncAction SendMessageAsync(string message, int replyToMessageId)
        {
            _log.TraceAsync($"Senden der Nachricht '{message}' mit Bezug zur Nachricht {replyToMessageId}");
            return _telebot.SendTextMessageAsync(_ChatID, message, replyToMessageId: replyToMessageId, replyMarkup: Helpers.KeyBoard.GetKeyboardByTelegramBotState(_state),
                cancellationToken: _ct).AsAsyncAction();
        }

        private void Initialize(BackgroundTaskDeferral deferral)
        {
            LoggingConfiguration logConf = new LoggingConfiguration();
#if DEBUG
            logConf.AddTarget(LogLevel.Trace, LogLevel.Fatal, new StreamingFileTarget());
            logConf.AddTarget(LogLevel.Trace, LogLevel.Fatal, new DebugTarget());
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
            bool serverAwake = await NetworkHelper.IsServerAwake();
            if (serverAwake != _serverAwake)
            {
                _serverAwake = serverAwake;
                string serverState = serverAwake ? "eingeschaltet" : "ausgeschlatet";
                string message = $"Der Server ist nun {serverState}! {Environment.NewLine} Erkannt durch Timer um {timer.Period.ToString()}";
                await _log.TraceAsync($"Toggle Server State via Timer {timer.Period.ToString()}");
                await SendMessageAsync(message);
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _log.InfoAsync($"Task wurde beendet, weil: {reason.ToString()}");
            _CheckServerStateTimer.Cancel();
            _ctSrc.Cancel();
            _ctSrc.Dispose();
            _deferral.Complete();
        }
    }
}
