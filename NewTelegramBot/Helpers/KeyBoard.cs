using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NewTelegramBot.Helpers
{
    static class KeyBoard
    {
        public static IReplyMarkup GetKeyboardByTelegramBotState(TelegramBotState state)
        {
            switch (state)
            {
                case TelegramBotState.DownloadMode:
                    return DownloadKeyboard();
                case TelegramBotState.DownloadQued:
                    return DownloadMessageKeyboard();
                case TelegramBotState.ConfigRouter:
                    return ConfigRouter();
                default:
                    return MainKeyboard();
            }
        }

        public static ReplyKeyboardMarkup MainKeyboard()
        {
            KeyboardButton b1 = new KeyboardButton("\ud83d\udd5a Server starten");
            KeyboardButton b2 = new KeyboardButton("\ud83d\udd17 Download Modus");
            KeyboardButton b3 = new KeyboardButton("\U00002328 Router-Konfig");
            KeyboardButton[] firstRow = new [] { b1 };
            KeyboardButton[] secondRow = new [] { b2 };
            KeyboardButton[] thirdRow = new [] { b3 };
            KeyboardButton[][] rows = new [] { firstRow, secondRow, thirdRow };
            return new ReplyKeyboardMarkup(rows, true);
        }

        public static InlineKeyboardMarkup DownloadMessageKeyboard()
        {
            InlineKeyboardButton b1 = new KeyboardButton("\U000023ef"); //PlayPause
            InlineKeyboardButton b2 = new KeyboardButton("\U000023ed"); //Next
            InlineKeyboardButton b3 = new KeyboardButton("\U000023f9"); //Stop
            InlineKeyboardButton[] row = new [] { b1, b2, b3 };
            return new InlineKeyboardMarkup(row);
        }

        public static ReplyKeyboardMarkup DownloadKeyboard()
        {
            KeyboardButton b1 = new KeyboardButton("\U0001f519 Download Modus beenden");
            KeyboardButton[] row = new [] { b1 };
            return new ReplyKeyboardMarkup(row, true);
        }

        public static ReplyKeyboardMarkup ConfigRouter()
        {
            KeyboardButton b1 = new KeyboardButton("\U0001f4f6 Gäste W-Lan");
            KeyboardButton b2 = new KeyboardButton("\U0001f534 WPS toggel");
            KeyboardButton b3 = new KeyboardButton("\U0001f519 Router-Konfig verlassen");
            KeyboardButton[] firstRow = new [] { b1, b2 };
            KeyboardButton[] lastRow = new [] { b3 };
            KeyboardButton[][] rows = new [] { firstRow, lastRow };
            return new ReplyKeyboardMarkup(rows, true);
        }
    }
}
