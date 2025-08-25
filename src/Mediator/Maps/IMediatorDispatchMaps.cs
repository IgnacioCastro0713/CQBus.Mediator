using System.Collections.Frozen;

namespace CQBus.Mediator.Maps;

public interface IMediatorDispatchMaps
{
    FrozenDictionary<(Type req, Type res), Delegate> Requests { get; }
    FrozenDictionary<Type, Delegate> Notifications { get; }
    FrozenDictionary<(Type req, Type res), Delegate> Streams { get; }
}
