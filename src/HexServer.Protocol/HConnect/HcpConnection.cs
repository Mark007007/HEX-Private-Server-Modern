using System.IO.Pipelines;
using HexServer.Core.Network;

namespace HexServer.Protocol.HConnect;

public sealed class HcpConnection : IAsyncDisposable
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;

    public HcpConnection(Stream stream)
    {
        _reader = PipeReader.Create(stream);
        _writer = PipeWriter.Create(stream);
    }

    public async IAsyncEnumerable<HcpFrame> ReadFramesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;
            try
            {
                while (HcpCodec.TryDecode(buffer, out var frame, out var consumed))
                {
                    buffer = buffer.Slice(consumed);
                    yield return frame;
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted)
                {
                    if (buffer.Length != 0)
                        throw new EndOfStreamException("Stream ended in the middle of an HCP frame.");
                    yield break;
                }
            }
            catch
            {
                _reader.AdvanceTo(buffer.Start, buffer.Start);
                throw;
            }
        }
    }

    public async ValueTask WriteFrameAsync(HcpFrame frame, CancellationToken cancellationToken = default)
    {
        var bytes = HcpCodec.Encode(frame);
        bytes.CopyTo(_writer.GetSpan(bytes.Length));
        _writer.Advance(bytes.Length);
        var flush = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (flush.IsCanceled)
            throw new OperationCanceledException(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _reader.CompleteAsync().ConfigureAwait(false);
        await _writer.CompleteAsync().ConfigureAwait(false);
    }
}
