using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opdeweg.Application.Features.Proximity;
using Opdeweg.Infrastructure.Redis;

namespace Opdeweg.Infrastructure.Services;

/// <summary>
/// The single writer for proximity membership. Drains the queue in small batches under a
/// distributed lease, coalescing repeated evaluations of the same driver.
/// </summary>
internal sealed partial class ProximityWorker(
    ChannelProximityCommandQueue queue,
    ProximityProcessor processor,
    IDistributedLock distributedLock,
    ILogger<ProximityWorker> logger) : BackgroundService
{
    private const int MaxBatchSize = 64;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<QueuedProximityCommand>(MaxBatchSize);

        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            batch.Clear();
            while (batch.Count < MaxBatchSize && queue.Reader.TryRead(out var item))
            {
                batch.Add(item);
            }

            try
            {
                await using var lease = await distributedLock.AcquireAsync("proximity", LeaseDuration, stoppingToken);
                await ProcessBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogBatchFailed(logger, ex, batch.Count);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            finally
            {
                foreach (var item in batch)
                {
                    item.Completion?.TrySetResult();
                }
            }
        }
    }

    private async Task ProcessBatchAsync(List<QueuedProximityCommand> batch, CancellationToken cancellationToken)
    {
        // Only the last evaluation of a driver in a batch matters; earlier ones complete with it.
        var lastEvaluation = new Dictionary<Guid, int>();
        for (var i = 0; i < batch.Count; i++)
        {
            if (batch[i].Command is EvaluateDriverCommand evaluate)
            {
                lastEvaluation[evaluate.UserId] = i;
            }
        }

        var reEvaluate = new HashSet<Guid>();
        for (var i = 0; i < batch.Count; i++)
        {
            var command = batch[i].Command;
            if (command is EvaluateDriverCommand e && lastEvaluation[e.UserId] != i)
            {
                continue;
            }

            try
            {
                reEvaluate.UnionWith(await processor.HandleAsync(command, cancellationToken));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCommandFailed(logger, ex, command.GetType().Name, command.UserId);
            }
        }

        foreach (var userId in reEvaluate)
        {
            queue.TryEnqueue(new EvaluateDriverCommand(userId));
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Proximity batch of {Count} commands failed")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Proximity command {Command} failed for user {UserId}")]
    private static partial void LogCommandFailed(ILogger logger, Exception exception, string command, Guid userId);
}
