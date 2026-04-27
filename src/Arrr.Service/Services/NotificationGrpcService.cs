using System.Collections.Concurrent;
using System.Threading.Channels;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Service.Grpc;
using Arrr.Service.Interfaces;
using Arrr.Service.Internal;
using Grpc.Core;

namespace Arrr.Service.Services;

internal sealed class NotificationGrpcService : NotificationService.NotificationServiceBase
{
    private readonly IConfigService _configService;
    private readonly IDndService _dndService;

    private readonly ConcurrentDictionary<string, (Channel<ArrEvent> Channel, IReadOnlyList<string> Sources)>
        _clients = new();

    public NotificationGrpcService(IEventBus eventBus, IConfigService configService, IDndService dndService)
    {
        _configService = configService;
        _dndService = dndService;
        eventBus.Subscribe<Notification>(OnNotificationAsync);
        eventBus.Subscribe<DndChangedEvent>(OnDndChangedAsync);
    }

    private async Task OnNotificationAsync(Notification n, CancellationToken ct)
    {
        var notifEvent = new NotificationEvent
        {
            Id        = n.Id.ToString(),
            Source    = n.Source,
            Title     = n.Title,
            Body      = n.Body,
            Timestamp = n.Timestamp.ToString("o"),
            IconUrl   = n.IconUrl ?? "",
            Priority  = (int)n.Priority,
            Url       = n.Url ?? "",
        };

        if (n.Extras != null)
        {
            foreach (var (k, v) in n.Extras)
            {
                notifEvent.Extras[k] = v;
            }
        }

        var evt = new ArrEvent { Notification = notifEvent };

        foreach (var (_, entry) in _clients)
        {
            if (entry.Sources.Count > 0 && !entry.Sources.Contains(n.Source))
            {
                continue;
            }

            await entry.Channel.Writer.WriteAsync(evt, ct);
        }
    }

    private async Task OnDndChangedAsync(DndChangedEvent e, CancellationToken ct)
    {
        var evt = new ArrEvent { Dnd = new DndEvent { Enabled = e.Enabled } };

        foreach (var (_, entry) in _clients)
        {
            await entry.Channel.Writer.WriteAsync(evt, ct);
        }
    }

    public override async Task Subscribe(
        SubscribeRequest request,
        IServerStreamWriter<ArrEvent> responseStream,
        ServerCallContext context)
    {
        var apiKey = context.RequestHeaders.GetValue("x-api-key");
        if (apiKey != _configService.Config.ApiKey)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid API key"));
        }

        var clientId = Guid.NewGuid().ToString();
        var channel  = Channel.CreateUnbounded<ArrEvent>();
        var sources  = (IReadOnlyList<string>)request.Sources.ToList();

        _clients[clientId] = (channel, sources);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(evt, context.CancellationToken);
            }
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            channel.Writer.TryComplete();
        }
    }

    public override Task<DndResponse> SetDnd(SetDndRequest request, ServerCallContext context)
    {
        var apiKey = context.RequestHeaders.GetValue("x-api-key");
        if (apiKey != _configService.Config.ApiKey)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid API key"));
        }

        _dndService.Set(request.Enabled);
        return Task.FromResult(new DndResponse { Enabled = _dndService.IsEnabled });
    }

    public override Task<DndResponse> GetDnd(GetDndRequest request, ServerCallContext context)
    {
        var apiKey = context.RequestHeaders.GetValue("x-api-key");
        if (apiKey != _configService.Config.ApiKey)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid API key"));
        }

        return Task.FromResult(new DndResponse { Enabled = _dndService.IsEnabled });
    }
}
