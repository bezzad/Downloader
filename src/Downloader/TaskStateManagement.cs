using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Downloader;

public class TaskStateManagement
{
    private readonly ConcurrentQueue<Exception> _exceptions = new ConcurrentQueue<Exception>();

    /// <summary>
    /// Gets the System.AggregateException that caused the ConcurrentStream
    /// to end prematurely. If the ConcurrentStream completed successfully
    /// or has not yet thrown any exceptions, this will return null.
    /// </summary>
    public AggregateException Exception { get; private set; }

    /// <summary>
    ///  Gets a value that indicates whether the task has completed.
    ///  Result is true if the task has completed (that is, the task is in one of the three final
    ///  states: TaskStatus.RanToCompletion, TaskStatus.Faulted or TaskStatus.Canceled); 
    ///  otherwise, false.
    /// </summary>
    public bool IsCompleted => Status == TaskStatus.RanToCompletion || Status == TaskStatus.Faulted || Status == TaskStatus.Canceled;

    /// <summary>
    /// Gets whether this ConcurrentStream.Task instance has completed execution
    /// due to being canceled.
    /// Result is true if the task has completed due to being canceled; otherwise false.
    /// </summary>
    public bool IsCanceled => Status == TaskStatus.Canceled;

    /// <summary>
    /// Gets whether the task ran to completion.
    /// Result is  true if the task ran to completion; otherwise false.
    /// </summary>
    public bool IsCompletedSuccessfully => Status == TaskStatus.RanToCompletion;

    /// <summary>
    /// Gets whether the ConcurrentStream.Task completed due to an unhandled exception.
    /// Reslt is true if the task has thrown an unhandled exception; otherwise false.
    /// </summary>    
    public bool IsFaulted => Status == TaskStatus.Faulted;

    /// <summary>
    /// Gets the TaskStatus of this task.
    /// </summary>
    public TaskStatus Status { get; private set; } = TaskStatus.Created;

    internal void StartState() => Status = TaskStatus.Running;
    internal void CompleteState() => Status = TaskStatus.RanToCompletion;
    internal void CancelState() => Status = TaskStatus.Canceled;
    internal void SetException(Exception exp)
    {
        Status = TaskStatus.Faulted;
        _exceptions.Enqueue(exp);
        Exception = new AggregateException(_exceptions);
    }
}
