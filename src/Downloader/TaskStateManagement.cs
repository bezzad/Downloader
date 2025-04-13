using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Manages the state and exceptions of a task.
/// </summary>
public class TaskStateManagement
{
    private readonly ConcurrentQueue<Exception> _exceptions = new();
    
    /// <summary>
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/> inject from DI in upper layers by user
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Gets the <see cref="System.AggregateException"/> that caused the task to end prematurely.
    /// If the task completed successfully or has not yet thrown any exceptions, this will return null.
    /// </summary>
    public AggregateException Exception { get; private set; }

    /// <summary>
    /// Gets a value that indicates whether the task has completed.
    /// Result is true if the task has completed (that is, the task is in one of the three final
    /// states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>, or <see cref="TaskStatus.Canceled"/>);
    /// otherwise, false.
    /// </summary>
    public bool IsCompleted => Status == TaskStatus.RanToCompletion || Status == TaskStatus.Faulted || Status == TaskStatus.Canceled;

    /// <summary>
    /// Gets whether this task instance has completed execution due to being canceled.
    /// Result is true if the task has completed due to being canceled; otherwise false.
    /// </summary>
    public bool IsCanceled => Status == TaskStatus.Canceled;

    /// <summary>
    /// Gets whether the task ran to completion.
    /// Result is true if the task ran to completion; otherwise false.
    /// </summary>
    public bool IsCompletedSuccessfully => Status == TaskStatus.RanToCompletion;

    /// <summary>
    /// Gets whether the task completed due to an unhandled exception.
    /// Result is true if the task has thrown an unhandled exception; otherwise false.
    /// </summary>
    public bool IsFaulted => Status == TaskStatus.Faulted;

    /// <summary>
    /// Gets the <see cref="TaskStatus"/> of this task.
    /// </summary>
    public TaskStatus Status { get; private set; } = TaskStatus.Created;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskStateManagement"/> class.
    /// </summary>
    /// <param name="logger">The logger to use for logging exceptions.</param>
    public TaskStateManagement(ILogger logger = null)
    {
        Logger = logger;
    }

    /// <summary>
    /// Sets the state of the task to <see cref="TaskStatus.Running"/>.
    /// </summary>
    internal void StartState() => Status = TaskStatus.Running;

    /// <summary>
    /// Sets the state of the task to <see cref="TaskStatus.RanToCompletion"/>.
    /// </summary>
    internal void CompleteState() => Status = TaskStatus.RanToCompletion;

    /// <summary>
    /// Sets the state of the task to <see cref="TaskStatus.Canceled"/>.
    /// </summary>
    internal void CancelState() => Status = TaskStatus.Canceled;

    /// <summary>
    /// Sets the state of the task to <see cref="TaskStatus.Faulted"/> and records the exception.
    /// </summary>
    /// <param name="exp">The exception that caused the task to fault.</param>
    /// <param name="callerName">The name of the caller method (automatically populated).</param>
    internal void SetException(Exception exp, [CallerMemberName] string callerName = null)
    {
        Logger?.LogCritical(exp, $"TaskStateManagement: SetException catch an exception on {callerName}");
        Status = TaskStatus.Faulted;
        _exceptions.Enqueue(exp);
        Exception = new AggregateException(_exceptions);
    }
}