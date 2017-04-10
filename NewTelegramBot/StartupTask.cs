using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Resources;
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
        private bool _serverAwake = false;
        private long _ChatID;
        private ResourceLoader _conf = ResourceLoader.GetForCurrentView("Config");
        private Helpers.RPCAria2Helper _aria2;

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
                            KeyboardButton t1;
                            KeyboardButton t2;
                            KeyboardButton[] row;
                            if (update.Type == UpdateType.MessageUpdate)
                            {
                                string msg = string.Empty;
                                if (message.Text.Equals("\U0001f5a5 Server starten"))
                                {
                                    //_serverAwake = await NetworkHelper.IsServerAwake();
                                    msg = "Der Server ist bereits an.";
                                    if (!_serverAwake)
                                    {
                                        //await NetworkHelper.WakeTheServer();
                                        msg = "Der Server wird gestartet";
                                    }
                                }
                                else if (message.Text.Equals("\U0001f517 Download Modus"))
                                {
                                    //Verweis auf andere Klasse mit DownloadLink
                                    
                                    Task t = _aria2.DownloadURI("https://github.com/aria2/aria2/releases/download/release-1.31.0/aria2-1.31.0-win-64bit-build1.zip", message.MessageId);
                                    msg = "Download-Test";
                                }
                                else
                                {
                                    msg = "Soll der Server gestartet werden?";
                                }
                                t1 = new KeyboardButton("\U0001f5a5 Server starten");
                                t2 = new KeyboardButton("\U0001f517 Download Modus");
                                row = new KeyboardButton[] { t1, t2 };
                                Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup test = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(row, true);
                                await _telebot.SendTextMessageAsync(message.Chat.Id, msg, false, false, 0, test, ParseMode.Default, _ct);
                            }
                            else if (update.Type == UpdateType.CallbackQueryUpdate)
                            {
                                t1 = new KeyboardButton("Hallo neues Keyboard");
                                row = new KeyboardButton[] { t1 };
                                Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup test = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(row, true);
                                await _telebot.SendTextMessageAsync(message.Chat.Id, update.CallbackQuery.Data, false, false, 0, test, ParseMode.Default, _ct);
                            }
                        }
                    }
                }
                await Task.Delay(new TimeSpan(0, 0, 10), _ct);
            }

            _deferral.Complete();
        }

        private void Initialize(BackgroundTaskDeferral deferral)
        {
            _deferral = deferral;
            _ctSrc = new CancellationTokenSource();
            _ct = _ctSrc.Token;
            _CheckServerStateTimer = ThreadPoolTimer.CreatePeriodicTimer(CheckServerStateTimerElapsedHandler, new TimeSpan(0, 5, 0));
            _telebot = new TelegramBotClient(_conf.GetString("TelegramToken"));
            _ChatID = Convert.ToInt64(_conf.GetString("TelegramChatID"));
            _aria2 = new Helpers.RPCAria2Helper(_ct, _conf);
        }

        private async void CheckServerStateTimerElapsedHandler(ThreadPoolTimer timer)
        {
            bool ttt = await NetworkHelper.IsServerAwake();
            if (ttt != _serverAwake)
            {
                string s = string.Empty;
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
