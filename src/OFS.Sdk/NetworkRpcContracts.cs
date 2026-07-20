namespace OFS.Sdk;

public enum ModNetworkRpcStatus
{
    Succeeded = 0,
    RemoteError = 1,
    TimedOut = 2,
    Cancelled = 3,
    ProtocolError = 4,
}

public readonly record struct ModNetworkRpcRequest<TRequest>(
    TRequest Value,
    INetworkPeer Sender,
    ModNetworkTransport Transport);

public readonly record struct ModNetworkRpcResult<TResponse>(
    ModNetworkRpcStatus Status,
    TResponse? Value,
    string? Error)
{
    public bool IsSuccess => Status == ModNetworkRpcStatus.Succeeded;
}

/// <summary>Server-side authorization decision made before an RPC handler runs.</summary>
public readonly record struct ModNetworkRpcAuthorizationResult(
    bool IsAuthorized,
    string? Error = null)
{
    public static ModNetworkRpcAuthorizationResult Allow() => new(true);

    public static ModNetworkRpcAuthorizationResult Deny(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new ModNetworkRpcAuthorizationResult(false, error);
    }
}

/// <summary>Per-connection token-bucket protection for one RPC endpoint.</summary>
public sealed record ModNetworkRpcRateLimit(
    int Burst = 32,
    double RefillPerSecond = 16,
    bool Enabled = true)
{
    public static ModNetworkRpcRateLimit Default { get; } = new();
    public static ModNetworkRpcRateLimit Unlimited { get; } = new(1, 1, false);
}

/// <summary>Definition for one typed client-to-server request/response endpoint.</summary>
public sealed record ModNetworkRpcDefinition<TRequest, TResponse>(
    string Id,
    Func<ModNetworkRpcRequest<TRequest>, TResponse> Handler,
    ModNetworkSerializer<TRequest>? RequestSerializer = null,
    ModNetworkSerializer<TResponse>? ResponseSerializer = null,
    int MaxRequestBytes = 16 * 1024,
    int MaxResponseBytes = 16 * 1024,
    TimeSpan? DefaultTimeout = null,
    Func<ModNetworkRpcRequest<TRequest>, ModNetworkRpcAuthorizationResult>? Authorize = null,
    ModNetworkRpcRateLimit? RateLimit = null);

/// <summary>A pending client-side RPC invocation.</summary>
public interface IModNetworkRpcCall
{
    uint RequestId { get; }
    bool IsPending { get; }
    void Cancel();
}

/// <summary>Owner-scoped typed RPC transported by Mirror.</summary>
public interface IModNetworkRpc<TRequest, TResponse> : IDisposable
{
    string OwnerId { get; }
    string Id { get; }
    string QualifiedId { get; }
    bool IsRegistered { get; }
    bool Enabled { get; set; }
    int PendingCount { get; }
    ModNetworkRpcRateLimit RateLimit { get; }

    IModNetworkRpcCall InvokeServer(
        TRequest request,
        Action<ModNetworkRpcResult<TResponse>> completed,
        TimeSpan? timeout = null,
        ModNetworkTransport transport = ModNetworkTransport.Reliable);

    void Unregister();
}
