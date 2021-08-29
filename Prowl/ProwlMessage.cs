using Prowl.Enums;
using Prowl.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Prowl
{
    public class ProwlMessage : IProwlMessage
    {
        private readonly HttpClient HttpClient;
        private readonly string ApiKey;

        public ProwlMessage()
        {
            HttpClient = new HttpClient();
            ApiKey = Environment.GetEnvironmentVariable("PROWL_API_KEY") ?? throw new ArgumentNullException(ApiKey);
        }

        public async Task<HttpResponseMessage> SendAsync(
            string description,
            Priority priority = Priority.Normal,
            string url = "",
            string application = "Prowl Message",
            string evnt = "Prowl Event")
        {
            string prowlAddUrl = "https://api.prowlapp.com/publicapi/add";

            var values = new Dictionary<string, string>
                {
                    { "apikey", ApiKey },
                    { "priority", priority.ToString() },
                    { "url", url },
                    { "application", application },
                    { "event", evnt },
                    { "description", description },
                };

            var content = new FormUrlEncodedContent(values);
            HttpResponseMessage response = await HttpClient.PostAsync(prowlAddUrl, content);
            return response;
        }

        public async Task<HttpResponseMessage> SendAsync(ProwlMessageContents prowlMessageContents)
        {
            return await SendAsync(
                prowlMessageContents.Description,
                prowlMessageContents.Priority,
                prowlMessageContents.Url,
                prowlMessageContents.Application,
                prowlMessageContents.Event);
        }
    }
}
