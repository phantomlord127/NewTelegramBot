using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewTelegramBot.Helpers.Json
{
    class JsonRessponse
    {
        [JsonProperty("jsonrpc", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public JToken JsonRPC { get;  set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public JToken Id { get; set; }

        [JsonProperty("method", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Method { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public JsonResponseParameters Parameters { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonResponseError Error { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
    class JsonResponseParameters
    {
        [JsonProperty("gid", NullValueHandling = NullValueHandling.Include)]
        public JToken Gid { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    class JsonResponseError
    {
        [JsonProperty("code", NullValueHandling = NullValueHandling.Include)]
        public JToken Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public JArray Date { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
