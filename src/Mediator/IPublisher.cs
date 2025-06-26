using CQBus.Mediator.Messages;

namespace CQBus.Mediator;

public interface IPublisher
{
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
