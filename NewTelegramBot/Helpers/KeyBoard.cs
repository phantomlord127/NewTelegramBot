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
    class KeyBoard
    {
        public static ReplyKeyboardMarkup GetKeyboardByTelegramBotState(TelegramBotState state)
        {
            if (state == TelegramBotState.DownloadMode)
            {
                return DownloadKeyboard();
            }
            return MainKeyboard();
        }

        public static ReplyKeyboardMarkup MainKeyboard()
        {
            KeyboardButton b1 = new KeyboardButton("\U0001f5a5 Server starten");
            KeyboardButton b2 = new KeyboardButton("\U0001f517 Download Modus");
            KeyboardButton[] row = new KeyboardButton[] { b1, b2 };
            return new ReplyKeyboardMarkup(row, true);
        }

        public static ReplyKeyboardMarkup DownloadKeyboard()
        {
            KeyboardButton b1 = new KeyboardButton("\U0001f519 Download Modus beenden");
            KeyboardButton[] row = new KeyboardButton[] { b1 };
            return new ReplyKeyboardMarkup(row, true);
        }
    }
}
