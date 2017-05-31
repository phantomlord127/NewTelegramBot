using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NewTelegramBot.Helpers.Json
{
    class JsonRessponse
    {
        [JsonProperty("jsonrpc", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string JsonRpc { get;  set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public long Id { get; set; }

        [JsonProperty("method", NullValueHandling = NullValueHandling.Ignore)]
        public string Method { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public JsonResponseParameters[] Parameters { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonResponseError Error { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public string Result { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
    class JsonResponseParameters
    {
        [JsonProperty("gid", NullValueHandling = NullValueHandling.Include)]
        public string Gid { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    class JsonResponseError
    {
        [JsonProperty("code", NullValueHandling = NullValueHandling.Include)]
        public int Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string Date { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
