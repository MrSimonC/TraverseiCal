using DurableTemplate;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Prowl;

[assembly: FunctionsStartup(typeof(Startup))]

namespace DurableTemplate;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddHttpClient();
        string prowlApiKey = Environment.GetEnvironmentVariable("PROWL_API_KEY") ?? throw new NullReferenceException("Missing PROWL_API_KEY");
        builder.Services.AddSingleton<IProwlMessage>(x => new ProwlMessage(prowlApiKey));
    }
}