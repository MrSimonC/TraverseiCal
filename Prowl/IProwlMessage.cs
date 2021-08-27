using Prowl.Enums;
using System.Net.Http;
using System.Threading.Tasks;

namespace Prowl
{
    public interface IProwlMessage
    {
        Task<HttpResponseMessage> SendAsync(string description, Priority priority = Priority.Normal, string url = "", string application = "Prowl Message", string @event = "Prowl Event");
    }
}