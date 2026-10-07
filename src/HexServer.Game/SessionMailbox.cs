using System.Threading.Channels;

namespace HexServer.Game;

public sealed class SessionMailbox<T>
{
    private readonly Channel<T> _channel;

    public SessionMailbox(int capacity = 256)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _channel = Channel.CreateBounded<T>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public ValueTask EnqueueAsync(
        T item,
        CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(item, cancellationToken);

    public void Complete(Exception? error = null)
        => _channel.Writer.TryComplete(error);

    public IAsyncEnumerable<T> ReadAllAsync(
        CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
