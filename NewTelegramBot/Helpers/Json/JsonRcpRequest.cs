using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewTelegramBot.Helpers.Json
{
    class JsonRcpRequest
    {
        [JsonProperty("jsonrpc", Required = Required.Always)]
        public JToken JsonRPC { get { return "2.0"; } }

        [JsonProperty("id", Required = Required.Always, NullValueHandling = NullValueHandling.Include)]
        public JToken Id { get; set; }

        [JsonProperty("method", Required = Required.Always)]
        public JToken Method { get; set; }

        [JsonProperty("params")]
        public JArray Params { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
