using DurableTemplate;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Prowl;

[assembly: FunctionsStartup(typeof(Startup))]

namespace DurableTemplate
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<IProwlMessage, ProwlMessage>();
        }
    }
}