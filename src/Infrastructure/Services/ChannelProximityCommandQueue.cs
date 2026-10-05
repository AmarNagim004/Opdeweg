using System.Threading.Channels;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Application.Interfaces;

namespace Opdeweg.Infrastructure.Services;

internal sealed record QueuedProximityCommand(ProximityCommand Command, TaskCompletionSource? Completion);

/// <summary>
/// Bounded in-process queue feeding the single proximity writer. Swapping this for a durable,
/// region-partitioned stream is how proximity moves into a dedicated service later.
/// </summary>
internal sealed class ChannelProximityCommandQueue : IProximityCommandQueue
{
    private readonly Channel<QueuedProximityCommand> _channel = Channel.CreateBounded<QueuedProximityCommand>(
        new BoundedChannelOptions(10_000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<QueuedProximityCommand> Reader => _channel.Reader;

    public bool TryEnqueue(ProximityCommand command) => _channel.Writer.TryWrite(new QueuedProximityCommand(command, null));

    public async Task<bool> EnqueueAndWaitAsync(ProximityCommand command, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(new QueuedProximityCommand(command, completion)))
        {
            return false;
        }

        try
        {
            await completion.Task.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
