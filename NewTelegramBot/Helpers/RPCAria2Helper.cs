using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NewTelegramBot.Helpers.Json;
using MetroLog;

namespace NewTelegramBot.Helpers
{
    class RpcAria2Helper : IDisposable
    {
        private MessageWebSocket _webSock = new MessageWebSocket();
        private DataWriter _messageWriter;
        readonly Uri _serverUri;
        readonly Dictionary<string, int> _downloads = new Dictionary<string, int>();
        readonly Dictionary<int, string> _downloadQue = new Dictionary<int, string>();
        private bool _isConnected;
        readonly object _monitor = new object();
        readonly string _Aria2Token;
        readonly StartupTask _telegramBot;
        readonly static ILoggerAsync _log = (ILoggerAsync)LogManagerFactory.DefaultLogManager.GetLogger<StartupTask>();

        public RpcAria2Helper(StartupTask telegramBot)
        {
            _telegramBot = telegramBot;
            _serverUri = new Uri(telegramBot.Config.GetString("Aria2URL"));
            _Aria2Token = telegramBot.Config.GetString("Aria2Secret");
            _log.TraceAsync("RPCAria2Helper initialisiert.");
        }

        public async Task DownloadUri(string downloadFile, int messageId)
        {
            if (await ConnectedToWebSocket())
            {
                if (_downloadQue.Count > 0)
                {
                    foreach (KeyValuePair<int, string> entry in _downloadQue)
                    {
                        await SendMessage(entry.Value, entry.Key).ConfigureAwait(false);
                    }
                    _downloadQue.Clear();
                }
                await SendMessage(downloadFile, messageId).ConfigureAwait(false);
            }
            else
            {
                lock (_monitor)
                {
                    _downloadQue.Add(messageId, downloadFile);
                }
            }
        }

        private async Task SendMessage(string downloadFile, int messageId)
        {
            string msg = await Task.Factory.StartNew(() => JsonConvert.SerializeObject(CreateRequestObject(downloadFile, messageId)));
            await _log.DebugAsync($"Aria2: Write Message: {msg}");
            _messageWriter.WriteString(msg);
            try
            {
                await _messageWriter.StoreAsync();
                await _messageWriter.FlushAsync();
                await _log.DebugAsync($"Aria2: Message send: {msg}");
            }
            catch (Exception ex)
            {
                await _log.ErrorAsync("Fehler beim Speichern des Json-Objects", ex);
            }
        }

        private void WebSock_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            CloseWebSocketConnection(args.Code, args.Reason);
        }

        private async void WebSock_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            using (DataReader reader = args.GetDataReader())
            {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                try
                {
                    string msg = string.Empty;
                    int messageID = 0;
                    string read = reader.ReadString(reader.UnconsumedBufferLength);
                    await _log.DebugAsync($"Aria2: Message received: {read}");
                    JsonRessponse response = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<JsonRessponse>(read));
                    if (response.Error == null && response.Parameters != null)
                    {
                        string gid = response.Parameters[0].Gid.ToString();
                        _downloads.TryGetValue(gid, out messageID);
                        if (response.Method.ToString() == "aria2.onDownloadStart")
                        {
                            await _telegramBot.SendMessageAsync("Download gestartet");
                        }
                        else
                        {
                            if (response.Method.ToString() == "aria2.onDownloadComplete")
                            {
                                msg = "Download beendet";
                                lock (_monitor)
                                {
                                    _downloads.Remove(gid);
                                }
                            }
                            else
                            {
                                msg = $"Nachricht zum Download: {response.Method.ToString()}";
                            }
                            await _telegramBot.SendMessageAsync(msg, messageID);
                        }
                        if (_downloads.Count == 0)
                        {
                            CloseWebSocketConnection();
                        }
                    }
                    else if (response.Error != null)
                    {
                        await _log.ErrorAsync($"Fehler bei Aria2:{response.ToString()}");
                        await _telegramBot.SendMessageAsync($"Nachricht zu diesem Download:{Environment.NewLine}{response.Error.ToString()}");
                    }
                    else
                    {
                        await _log.InfoAsync($"Aria2 Nachricht ohne Auswirkung:{response.ToString()}");
                    }
                }
                catch (Exception ex)
                {
                    await _log.ErrorAsync("Fehler beim Lesen des Json-Objects", ex);
                }
            }
        }

        private async Task<bool> ConnectedToWebSocket()
        {
            if (!_isConnected)
            {
                _webSock = new MessageWebSocket();
                _webSock.Control.MessageType = SocketMessageType.Utf8;
                _webSock.MessageReceived += WebSock_MessageReceived;
                _webSock.Closed += WebSock_Closed;
                _messageWriter = new DataWriter(_webSock.OutputStream);
                try
                {
                    await _webSock.ConnectAsync(_serverUri);
                    _isConnected = true;
                }
                catch (Exception ex)
                {
                    await _log.ErrorAsync("Fehler beim Herstellen der Verbindung zum Server.", ex);
                    CloseWebSocketConnection();
                    _isConnected = false;
                }
            }
            return _isConnected;
        }

        private void CloseWebSocketConnection(ushort code = 1000, string reason = "Closed due to user request.")
        {
            if (_webSock != null)
            {
                try
                {
                    _messageWriter.DetachStream();
                    _messageWriter.Dispose();
                    _messageWriter = null;
                    _webSock.Close(code, reason);
                    _log.InfoAsync("Verbindung zu Aria2 geschlossen.");
                }
                catch (Exception ex)
                {
                    _log.ErrorAsync("Fehler beim Schließen der Verbindung zum Server.", ex);
                }
            }
            _webSock = null;
            _isConnected = false;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            _log.InfoAsync("Disposing RpcAria2Helper");
            CloseWebSocketConnection();
            _downloadQue.Clear();
            _downloads.Clear();
        }

        private JObject CreateRequestObject(string downloadFile, int messageId)
        {
            string gid = Guid.NewGuid().ToString("n").Substring(0, 16);
            JObject jsonObject = new JObject();
            JObject opt = new JObject();
            JArray uris = new JArray();
            JArray parameters = new JArray();
            jsonObject["jsonrpc"] = "2.0";
            jsonObject["id"] = DateTime.Now.Ticks;
            jsonObject["method"] = "aria2.addUri";
            jsonObject["params"] = parameters;
            parameters.Add(_Aria2Token);
            parameters.Add(uris);
            parameters.Add(opt);
            uris.Add(downloadFile);
            opt["gid"] = gid;
            lock (_monitor)
            {
                _downloads.Add(gid, messageId);
            }
            return jsonObject;
        }
    }
}
