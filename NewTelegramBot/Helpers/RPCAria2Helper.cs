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
    class RPCAria2Helper : IDisposable
    {
        private MessageWebSocket _webSock = new MessageWebSocket();
        private DataWriter _messageWriter;
        private Uri _serverUri;
        readonly Dictionary<string, int> _downloads = new Dictionary<string, int>();
        readonly Dictionary<int, string> _downloadQue = new Dictionary<int, string>();
        private bool _isConnected;
        readonly CancellationToken _ct;
        readonly object _monitor = new object();
        readonly string _Aria2Token;
        readonly StartupTask _telegramBot;
        readonly static ILoggerAsync _log = (ILoggerAsync)LogManagerFactory.DefaultLogManager.GetLogger<StartupTask>();

        public RPCAria2Helper(CancellationToken ct, StartupTask telegramBot)
        {
            _ct = ct;
            _telegramBot = telegramBot;
            _serverUri = new Uri(telegramBot.Config.GetString("Aria2URL"));
            _Aria2Token = telegramBot.Config.GetString("Aria2Secret");
            _log.TraceAsync("RPCAria2Helper initialisiert.");
        }

        public async Task DownloadURI(string downloadUri, int messageId)
        {
            if (await ConnectedToWebSocket())
            {
                JArray request = new JArray();
                if (_downloadQue.Count > 0)
                {
                    foreach (KeyValuePair<int, string> entry in _downloadQue)
                    {
                        request.Add(CreateRequestObject(entry.Value, entry.Key));
                    }
                    _downloadQue.Clear();
                }

                request.Add(CreateRequestObject(downloadUri, messageId));
                _messageWriter.WriteString(await Task.Factory.StartNew(() => JsonConvert.SerializeObject(request)));
                try
                {
                    await _messageWriter.StoreAsync();
                }
                catch (Exception ex)
                {
                    await _log.ErrorAsync("Fehler beim Speichern des Json-Objects", ex);
                }
            }
            else
            {
                lock (_monitor)
                {
                    _downloadQue.Add(messageId, downloadUri);
                }
            }
        }

        private void WebSock_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            CloseWebSocketConnection();
        }

        private async void WebSock_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            using (DataReader reader = args.GetDataReader())
            {
                reader.UnicodeEncoding = UnicodeEncoding.Utf8;
                try
                {
                    string read = reader.ReadString(reader.UnconsumedBufferLength);
                    JsonRessponse response = await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<JsonRessponse>(read));
                    if (response.Error == null)
                    {
                        string gid = response.Parameters[0].Gid.ToString();
                        int messageID = _downloads[gid];
                        // Wenn download Start
                        await _telegramBot.SendMessageAsync($"Nachricht zu diesem Download:{Environment.NewLine}{response.Method.ToString()}");

                        // Wenn Dowload Ende
                        if (response.Method.ToString() == "aria2.onDownloadComplete()")
                        {
                            lock (_monitor)
                            {
                                _downloads.Remove(gid);
                            }
                        }


                        if (_downloads.Count == 0)
                        {
                            CloseWebSocketConnection();
                        }
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

        private void CloseWebSocketConnection()
        {
            if (_webSock != null)
            {
                try
                {
                    _webSock.Close(1000, "All Downloads fineshed.");
                }
                catch (Exception ex)
                {
                    _log.ErrorAsync("Fehler beim Schließen der Verbindung zum Server.", ex);
                }
                _webSock.Dispose();
            }
            _webSock = null;
            _isConnected = false;
        }

        public void Dispose()
        {
            CloseWebSocketConnection();
        }

        private JObject CreateRequestObject(string downloadUri, int messageId)
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
            uris.Add(downloadUri);
            opt["gid"] = gid;
            lock (_monitor)
            {
                _downloads.Add(gid, messageId);
            }
            return jsonObject;
        }
    }
}
