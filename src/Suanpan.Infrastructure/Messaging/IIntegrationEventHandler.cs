using Suanpan.Shared.Messages;

namespace Suanpan.Infrastructure.Messaging;

/// <summary>消息处理器。一般继承 <see cref="IntegrationEventHandler{TMessage}"/>，用 AddIntegrationEventHandler 注册</summary>
public interface IIntegrationEventHandler<in TMessage> where TMessage : IntegrationMessage
{
    Task HandleAsync(TMessage message, CancellationToken ct);
}
