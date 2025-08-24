using System.Collections.Frozen;

namespace CQBus.Mediator.Maps;

internal sealed class MediatorDispatchMaps(
    FrozenDictionary<(Type, Type), Delegate> requests,
    FrozenDictionary<Type, Delegate> notifications,
    FrozenDictionary<(Type, Type), Delegate> streams) : IMediatorDispatchMaps
{
    public FrozenDictionary<(Type, Type), Delegate> Requests { get; } = requests;
    public FrozenDictionary<Type, Delegate> Notifications { get; } = notifications;
    public FrozenDictionary<(Type, Type), Delegate> Streams { get; } = streams;
}
