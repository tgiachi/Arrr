using System.Text;
using Arrr.Core.Data.Notifications;
using Arrr.Core.Interfaces;
using Arrr.Core.Utils;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MqttSource.Data;

namespace MqttSource;

public class MqttSourcePlugin : ISourcePlugin, IConfigurablePlugin, IDisposable
{
    private readonly IMqttClient? _injectedClient;

    private IMqttClient? _client;
    private MqttSourceConfig _config = new();
    private IPluginContext? _context;

    public string Id => "com.arrr.plugin.mqtt";
    public string Name => "MQTT";
    public string Version => VersionUtils.Get(typeof(MqttSourcePlugin));
    public string Author => "tom (tom@orivega.io)";
    public string Description => "Subscribes to MQTT topics and emits a notification for each received message.";
    public string[] Categories => ["iot", "messaging"];
    public string Icon => "📡";
    public Type ConfigType => typeof(MqttSourceConfig);

    public MqttSourcePlugin() { }

    internal MqttSourcePlugin(IMqttClient client)
    {
        _injectedClient = client;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _client = null;
    }

    public async Task StartAsync(IPluginContext context, CancellationToken ct)
    {
        _context = context;
        _config = await context.LoadConfigAsync<MqttSourceConfig>(ct);

        var clientId = string.IsNullOrEmpty(_config.ClientId)
                           ? $"arrr-{Guid.NewGuid():N}"
                           : _config.ClientId;

        _client = _injectedClient ?? new MqttClientFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        var optionsBuilder = new MqttClientOptionsBuilder()
                             .WithTcpServer(_config.BrokerHost, _config.BrokerPort)
                             .WithClientId(clientId);

        if (!string.IsNullOrEmpty(_config.Username))
        {
            optionsBuilder = optionsBuilder.WithCredentials(_config.Username, _config.Password);
        }

        if (_config.UseTls)
        {
            optionsBuilder = optionsBuilder.WithTlsOptions(o => o.UseTls());
        }

        var options = optionsBuilder.Build();

        try
        {
            await _client.ConnectAsync(options, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                ex,
                "MQTT plugin failed to connect to {Host}:{Port}",
                _config.BrokerHost,
                _config.BrokerPort
            );

            return;
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                                                .WithTopicFilter(f => f.WithTopic(_config.Topic))
                                                .Build();

        await _client.SubscribeAsync(subscribeOptions, ct);

        context.Logger.LogInformation(
            "MQTT plugin connected to {Host}:{Port}, subscribed to '{Topic}'",
            _config.BrokerHost,
            _config.BrokerPort,
            _config.Topic
        );

        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        await _client.DisconnectAsync(cancellationToken: CancellationToken.None);
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        if (_context is null)
        {
            return Task.CompletedTask;
        }

        var topic = e.ApplicationMessage.Topic;
        var seq = e.ApplicationMessage.Payload;
        var payload = seq.IsEmpty ? "" : Encoding.UTF8.GetString(seq.FirstSpan);

        var title = _config.TitleTemplate
                           .Replace("{topic}", topic)
                           .Replace("{payload}", payload);

        var notification = new Notification(
            Guid.NewGuid(),
            Id,
            title,
            payload,
            DateTimeOffset.UtcNow,
            null,
            Extras: new Dictionary<string, string>
            {
                ["mqtt.topic"] = topic,
            }
        );

        return _context.EventBus.PublishAsync(notification, CancellationToken.None);
    }
}
