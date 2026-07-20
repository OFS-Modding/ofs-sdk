using System.Text.Json;

namespace OFS.Sdk;

/// <summary>Serializer used by a replicated state value.</summary>
public sealed record ModNetworkSerializer<T>(
    Func<T, byte[]> Serialize,
    Func<ReadOnlyMemory<byte>, T> Deserialize);

/// <summary>Built-in serializers for replicated state.</summary>
public static class ModNetworkSerializers
{
    /// <summary>Creates a UTF-8 JSON serializer with frozen options.</summary>
    public static ModNetworkSerializer<T> Json<T>(JsonSerializerOptions? options = null)
    {
        var frozenOptions = options is null
            ? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            : new JsonSerializerOptions(options);
        frozenOptions.MakeReadOnly(populateMissingResolver: true);
        return new ModNetworkSerializer<T>(
            value => JsonSerializer.SerializeToUtf8Bytes(value, frozenOptions),
            payload => JsonSerializer.Deserialize<T>(payload.Span, frozenOptions)!);
    }
}

public enum ModReplicatedStateUpdateOrigin
{
    ServerSet = 0,
    RemoteSnapshot = 1,
}

public readonly record struct ModReplicatedStateUpdate<T>(
    T PreviousValue,
    T Value,
    ulong Revision,
    ModReplicatedStateUpdateOrigin Origin);

/// <summary>Definition for one server-authoritative replicated value.</summary>
public sealed record ModReplicatedStateDefinition<T>(
    string Id,
    T InitialValue,
    Action<ModReplicatedStateUpdate<T>>? Updated = null,
    ModNetworkSerializer<T>? Serializer = null,
    int MaxValueBytes = 16 * 1024,
    bool DisableOnException = true);

/// <summary>A typed server-authoritative value synchronized through Mirror.</summary>
public interface IModReplicatedState<T> : IDisposable
{
    string OwnerId { get; }
    string Id { get; }
    string QualifiedId { get; }
    T Value { get; }
    ulong Revision { get; }
    bool IsSynchronized { get; }
    bool IsRegistered { get; }
    bool Enabled { get; set; }

    /// <summary>Changes the authoritative value and broadcasts a new revision.</summary>
    void SetServer(
        T value,
        ModNetworkTransport transport = ModNetworkTransport.Reliable);

    /// <summary>Rebroadcasts the current authoritative revision.</summary>
    void BroadcastCurrent(
        ModNetworkTransport transport = ModNetworkTransport.Reliable);

    /// <summary>Requests the current value from the active server.</summary>
    void RequestSync();

    void Unregister();
}
