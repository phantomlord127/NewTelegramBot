using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NewTelegramBot.Helpers.Json;

namespace NewTelegramBot.Helpers
{
    class RPCAria2Helper : IDisposable
    {
        private MessageWebSocket _webSock = new MessageWebSocket();
        private DataWriter _messageWriter;
        private Uri _serverUri;
        private Dictionary<string, long> _downloads = new Dictionary<string, long>();
        private Dictionary<long, string> _downloadQue = new Dictionary<long, string>();
        private bool _isConnected = false;
        private CancellationToken _ct;
        private object _monitor = new object();
        private string _Aria2Token;

        public RPCAria2Helper(CancellationToken ct, ResourceLoader config)
        {
            _ct = ct;
            _serverUri = new Uri(config.GetString("Aria2URL"));
            _Aria2Token = config.GetString("Aria2Secret");
        }

        public async Task DownloadURI(string downloadUri, long messageId)
        {
            bool isConnected = await ConnectedToWebSocket();
            if (isConnected)
            {
                JObject jsonObject = new JObject();
                JObject opt = new JObject();
                JArray uris = new JArray();
                JArray parameters = new JArray();
                string guid = Guid.NewGuid().ToString("n").Substring(0, 16);
                jsonObject["jsonrpc"] = "2.0";
                //jsonObject["id"] = "a";
                jsonObject["method"] = "aria2.addUri";
                jsonObject["params"] = parameters;
                parameters.Add(_Aria2Token);
                parameters.Add(uris);
                parameters.Add(opt);
                uris.Add(downloadUri);
                opt["gid"] = guid;
                string jsonRequest = await Task.Factory.StartNew(() => JsonConvert.SerializeObject(jsonObject));
                _messageWriter.WriteString(jsonRequest);
                lock (_monitor)
                {
                    _downloads.Add(guid, messageId);
                }
                try
                {
                    await _messageWriter.StoreAsync();
                }
                catch (Exception ex)
                {
                    string s = ex.Message;
                }
            }
            else
            {
                _downloadQue.Add(messageId, downloadUri);
                //Nachricht schicken
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
                        string gid = response.Parameters.Gid.ToString();
                        long messageID = _downloads[gid];
                        // Wenn download Start


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
                    string s = ex.Message;
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
                    string s = string.Empty;
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
    }
}
