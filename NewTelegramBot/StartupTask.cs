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
        private Helpers.RPCAria2Helper _aria2;
        private TelegramBotState _state;
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
                        Message message = null;
                        if (update.Type == UpdateType.MessageUpdate)
                        {
                            message = update.Message;
                        }
                        else if (update.Type == UpdateType.CallbackQueryUpdate)
                        {
                            message = update.CallbackQuery.Message;
                        }

                        if (message.Chat.Id == _ChatID)
                        {
                            await _telebot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing, _ct).ConfigureAwait(false);
                            if (update.Type == UpdateType.MessageUpdate)
                            {
                                string msg = string.Empty;
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
                                else
                                {
                                    if (_state == TelegramBotState.DownloadMode)
                                    {
                                        await _aria2.DownloadURI(message.Text, message.MessageId);
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
            return _telebot.SendTextMessageAsync(_ChatID, message, replyToMessageId: replyToMessageId, replyMarkup: Helpers.KeyBoard.GetKeyboardByTelegramBotState(_state),
                cancellationToken: _ct).AsAsyncAction();
        }

        private void Initialize(BackgroundTaskDeferral deferral)
        {
            _deferral = deferral;
            _ctSrc = new CancellationTokenSource();
            _ct = _ctSrc.Token;
            _conf = ResourceLoader.GetForCurrentView("Config");
            _CheckServerStateTimer = ThreadPoolTimer.CreatePeriodicTimer(CheckServerStateTimerElapsedHandler, new TimeSpan(0, 5, 0));
            _telebot = new TelegramBotClient(_conf.GetString("TelegramToken"));
            _ChatID = Convert.ToInt64(_conf.GetString("TelegramChatID"));
            _aria2 = new Helpers.RPCAria2Helper(_ct, this);
            _serverAwake = false;
            _state = TelegramBotState.None;
        }

        private async void CheckServerStateTimerElapsedHandler(ThreadPoolTimer timer)
        {
            bool serverAwake = await NetworkHelper.IsServerAwake();
            if (serverAwake != _serverAwake)
            {
                _serverAwake = serverAwake;
                string message = string.Empty;
                if (serverAwake)
                {
                    message = "Der Server ist nun eingeschaltet!";
                }
                else
                {
                    message = "Der Server ist nun ausgeschlatet!";
                }
                await SendMessageAsync(message);
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _CheckServerStateTimer.Cancel();
            _ctSrc.Cancel();
            _deferral.Complete();
        }
    }
}
