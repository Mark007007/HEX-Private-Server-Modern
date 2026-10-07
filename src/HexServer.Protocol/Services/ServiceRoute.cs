namespace HexServer.Protocol.Services;

public interface IServiceHandler
{
    ValueTask<byte[]> HandleAsync(
        int methodId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}

public sealed class ServiceRouter
{
    private readonly Dictionary<int, IServiceHandler> _handlers = new();

    public void Register(int serviceId, IServiceHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(serviceId, handler))
            throw new InvalidOperationException($"Service {serviceId} is already registered.");
    }

    public ValueTask<byte[]> DispatchAsync(
        int serviceId,
        int methodId,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(serviceId, out var handler))
            throw new KeyNotFoundException($"Unknown HEX service id: {serviceId}");

        return handler.HandleAsync(methodId, payload, cancellationToken);
    }
}
