using HexServer.Protocol.HConnect;

namespace HexServer.Protocol.Services;

public sealed record ServiceResponse(
    int DataType,
    ulong? RoutingPlayerId,
    byte[] Payload,
    byte Compression = 0);

public interface IServiceHandler
{
    ValueTask<ServiceResponse> HandleAsync(
        HcpServiceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ServiceRouter
{
    private readonly Dictionary<int, IServiceHandler> _handlers = new();

    public void Register(int serviceId, IServiceHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(serviceId, handler))
            throw new InvalidOperationException(
                $"Service {serviceId} is already registered.");
    }

    public ValueTask<ServiceResponse> DispatchAsync(
        HcpServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(request.ServiceId, out var handler))
            throw new KeyNotFoundException(
                $"Unknown HEX service id: {request.ServiceId}");

        return handler.HandleAsync(request, cancellationToken);
    }
}
