using Arrr.Core.Services;

namespace Arrr.Service.Services;

public class EventBusHostedService : IHostedService
{
    private readonly EventBusService _eventBusService;

    public EventBusHostedService(EventBusService eventBusService)
    {
        _eventBusService = eventBusService;
    }

    public Task StartAsync(CancellationToken ct)
        => _eventBusService.StartAsync(ct);

    public Task StopAsync(CancellationToken ct)
        => _eventBusService.StopAsync(ct);
}
