using Arrr.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Arrr.Service.Hubs;

internal class NotificationStreamHub : Hub
{
    private readonly IConfigService _configService;

    public NotificationStreamHub(IConfigService configService)
    {
        _configService = configService;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var provided = http?.Request.Query["key"].ToString()
                       ?? http?.Request.Headers["X-Api-Key"].ToString()
                       ?? "";

        var expected = _configService.Config.ApiKey;

        if (expected != "" && provided != expected)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}
