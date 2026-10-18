using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Suanpan.Infrastructure.Messaging;
using Suanpan.Infrastructure.Tests.Fixtures;
using Suanpan.Shared.Messages;
using Xunit;

namespace Suanpan.Infrastructure.Tests.Messaging;

/// <summary>
/// 事件总线的收发、重试和延时投递。每个测试用自己的消息类型和程序名，队列互不影响。
/// 等待消息用 TaskCompletionSource 加超时，不定时轮询（后端约定 §13.4）。
/// </summary>
[Trait(TestCategory.Key, TestCategory.Integration)]
public class EventBusTests(RabbitMqFixture rabbitMq)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Publish_DeliversOneCopyToEachSubscribedProgram()
    {
        await rabbitMq.DeclareQueueAsync("job", nameof(DeliveredEvent), Ct);
        await rabbitMq.DeclareQueueAsync("portal", nameof(DeliveredEvent), Ct);
        using var job = await StartAppAsync("job", s => s.AddIntegrationEventHandler<DeliveredEvent, RecordingHandler<DeliveredEvent>>());
        using var portal = await StartAppAsync("portal", s => s.AddIntegrationEventHandler<DeliveredEvent, RecordingHandler<DeliveredEvent>>());
        var message = new DeliveredEvent("hello");

        await job.Services.GetRequiredService<IEventBus>().PublishAsync(message, ct: Ct);

        Assert.Equal(message, await Recorder(job).Received.WaitAsync(Timeout, Ct));
        Assert.Equal(message, await Recorder(portal).Received.WaitAsync(Timeout, Ct));
    }

    /// <summary>失败后重试 3 次（共处理 4 次），之后转入 .failed 队列</summary>
    [Fact]
    public async Task FailingHandler_IsRetriedThreeTimes_ThenMovedToFailedQueue()
    {
        const string appName = "collector";
        var queue = MessageQueueNames.Queue(appName, nameof(FailingCommand));
        await rabbitMq.DeclareQueueAsync(appName, nameof(FailingCommand), Ct);
        using var app = await StartAppAsync(appName, s => s.AddIntegrationEventHandler<FailingCommand, ThrowingHandler>());
        await using var failedChannel = await rabbitMq.CreateChannelAsync(Ct);
        await failedChannel.QueueDeclareAsync(MessageQueueNames.Failed(queue), durable: true, exclusive: false, autoDelete: false, cancellationToken: Ct);
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(failedChannel);
        consumer.ReceivedAsync += (_, args) =>
        {
            failed.TrySetResult(Encoding.UTF8.GetString(args.Body.Span));
            return Task.CompletedTask;
        };
        await failedChannel.BasicConsumeAsync(MessageQueueNames.Failed(queue), autoAck: true, consumer, Ct);
        var message = new FailingCommand(42);

        await app.Services.GetRequiredService<IEventBus>().PublishAsync(message, ct: Ct);

        Assert.Contains(message.MessageId.ToString(), await failed.Task.WaitAsync(Timeout, Ct));
        Assert.Equal(4, Recorder(app).Attempts);
    }

    [Fact]
    public async Task DelayedPublish_IsDeliveredAfterDelay()
    {
        await rabbitMq.DeclareQueueAsync("crm", nameof(DelayedEvent), Ct);
        using var app = await StartAppAsync("crm", s => s.AddIntegrationEventHandler<DelayedEvent, RecordingHandler<DelayedEvent>>());
        var message = new DelayedEvent(7);

        await app.Services.GetRequiredService<IEventBus>().PublishAsync(message, TimeSpan.FromSeconds(1), Ct);

        Assert.Equal(message, await Recorder(app).Received.WaitAsync(Timeout, Ct));
    }

    public sealed record DeliveredEvent(string Text) : IntegrationMessage;

    public sealed record FailingCommand(int Value) : IntegrationMessage;

    public sealed record DelayedEvent(int Value) : IntegrationMessage;

    /// <summary>每个程序（host）一份，记录收到的消息和处理次数</summary>
    public sealed class MessageRecorder
    {
        private readonly TaskCompletionSource<IntegrationMessage> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        public Task<IntegrationMessage> Received => _received.Task;

        public int Attempts => Volatile.Read(ref _attempts);

        public void Record(IntegrationMessage message)
        {
            Interlocked.Increment(ref _attempts);
            _received.TrySetResult(message);
        }
    }

    public sealed class RecordingHandler<TMessage>(MessageRecorder recorder) : IntegrationEventHandler<TMessage>
        where TMessage : IntegrationMessage
    {
        public override Task HandleAsync(TMessage message, CancellationToken ct)
        {
            recorder.Record(message);
            return Task.CompletedTask;
        }
    }

    public sealed class ThrowingHandler(MessageRecorder recorder) : IntegrationEventHandler<FailingCommand>
    {
        public override Task HandleAsync(FailingCommand message, CancellationToken ct)
        {
            recorder.Record(message);
            throw new InvalidOperationException("模拟处理失败");
        }
    }

    private async Task<IHost> StartAppAsync(string appName, Action<IServiceCollection> subscribe)
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Uri"] = await rabbitMq.GetUriAsync(),
            ["RabbitMq:RetryDelaySeconds"] = "1",
        });
        builder.Services.AddSuanpanEventBus(builder.Configuration, appName);
        builder.Services.AddSingleton<MessageRecorder>();
        subscribe(builder.Services);

        var host = builder.Build();
        await host.StartAsync(Ct);
        return host;
    }

    private static MessageRecorder Recorder(IHost host) => host.Services.GetRequiredService<MessageRecorder>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
