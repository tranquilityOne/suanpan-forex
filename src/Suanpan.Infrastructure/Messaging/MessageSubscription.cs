namespace Suanpan.Infrastructure.Messaging;

/// <summary>订阅登记：AddIntegrationEventHandler 时写入容器，接收端据此声明队列</summary>
public sealed record MessageSubscription(Type MessageType, Type HandlerType);
