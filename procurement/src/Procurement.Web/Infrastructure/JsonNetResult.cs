using System;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Procurement.Web.Infrastructure
{
    /// <summary>JSON with ISO dates (MVC's own Json() writes "/Date(...)/"), for the grids.</summary>
    public class JsonNetResult : ActionResult
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new IsoDateTimeConverter { DateTimeFormat = "yyyy-MM-ddTHH:mm:ss" } }
        };

        public JsonNetResult(object data, int statusCode = 200)
        {
            Data = data;
            StatusCode = statusCode;
        }

        public object Data { get; }
        public int StatusCode { get; }

        public override void ExecuteResult(ControllerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCode;
            response.ContentType = "application/json";
            response.Write(JsonConvert.SerializeObject(Data, Settings));
        }
    }
}
