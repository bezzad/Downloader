using System.Collections.Concurrent;

namespace Downloader;

/// <summary>
/// Hands the whole-file progress from the storage writer to <see cref="IDownloadService.DownloadProgressChanged"/>
/// (issue #256). The writer reports every data block right after it is written to the storage, and never waits
/// for the event handler: ONE deliverer, on the thread pool, raises the event, one call at a time and in order.
/// <list type="bullet">
/// <item>Normal mode keeps only the newest total. A slow handler gets fewer events, never older ones.</item>
/// <item>Live-streaming mode keeps every block, in order, because the handler needs all the bytes. When the
/// handler is slower than the download and the blocks waiting pass the memory limit, the chunk stops reading
/// from the network until the handler catches up (see <see cref="StreamRoom"/>). A limit of 0 means no limit.</item>
/// </list>
/// </summary>
internal sealed class ProgressOutbox(Action<long, long, Memory<byte>> raise, Func<bool> hasHandler)
{
    private readonly ConcurrentQueue<LiveBlock> _liveBlocks = new();
    private readonly PauseTokenSource _streamRoom = new();
    private bool _liveStreaming; // set by Start, read by the writer thread
    private long _liveLimit; // set by Start, read by the writer and the deliverer
    private long _liveBytes; // bytes of the blocks in _liveBlocks
    private long _written; // bytes on the storage; only the writer thread changes it while a download runs
    private long _undelivered; // normal mode: bytes written but not reported yet
    private int _delivering; // 1 while a deliverer runs

    /// <summary>
    /// Paused while live-streaming blocks wait for the handler beyond the memory limit.
    /// A chunk waits on it before it reads the next block from the network.
    /// </summary>
    public PauseToken StreamRoom => _streamRoom.Token;

    /// <summary>
    /// Prepares a download run. Call it while nothing is being written.
    /// </summary>
    /// <param name="writtenBytes">The bytes already on the storage (a resumed download).</param>
    /// <param name="liveStreaming">Whether every block must reach the handler.</param>
    /// <param name="liveLimit">The most live-streaming bytes that may wait for the handler; 0 is no limit.</param>
    /// <param name="newFile">A new file drops the blocks of the last file the handler did not take yet.
    /// A resume keeps them: they are the bytes just before the resume point.</param>
    public void Start(long writtenBytes, bool liveStreaming, long liveLimit, bool newFile)
    {
        if (newFile)
            DropUndelivered();

        _liveStreaming = liveStreaming;
        Volatile.Write(ref _liveLimit, liveLimit);
        Volatile.Write(ref _written, writtenBytes);
        UpdateStreamRoom();
    }

    /// <summary>
    /// Called by the storage writer after a data block is written. Never waits.
    /// </summary>
    /// <param name="length">The length of the block.</param>
    /// <param name="liveData">A copy of the block for live streaming, or empty.</param>
    /// <returns>The bytes on the storage now.</returns>
    public long OnWritten(int length, Memory<byte> liveData)
    {
        long written = Volatile.Read(ref _written) + length;
        if (hasHandler())
        {
            if (_liveStreaming)
            {
                // Queue the block BEFORE the total moves on, so a total the deliverer reads never
                // counts a block it has not seen yet.
                _liveBlocks.Enqueue(new LiveBlock(written, liveData));
                Interlocked.Add(ref _liveBytes, length);
                UpdateStreamRoom();
            }
            else
            {
                Interlocked.Add(ref _undelivered, length);
            }
        }

        Volatile.Write(ref _written, written);
        if (HasWork)
            TryStartDeliverer();

        return written;
    }

    /// <summary>
    /// Drops everything the handler did not take yet, and lets a waiting chunk go on.
    /// </summary>
    public void DropUndelivered()
    {
        while (_liveBlocks.TryDequeue(out LiveBlock block))
            Interlocked.Add(ref _liveBytes, -block.Data.Length);

        Interlocked.Exchange(ref _undelivered, 0);
        _streamRoom.Resume();
    }

    /// <summary>
    /// True when every update so far has reached the handler.
    /// </summary>
    public bool IsIdle => Volatile.Read(ref _delivering) == 0 && !HasWork;

    private bool HasWork => !_liveBlocks.IsEmpty || Volatile.Read(ref _undelivered) > 0;

    private void TryStartDeliverer()
    {
        if (Interlocked.CompareExchange(ref _delivering, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(_ => Deliver());
    }

    private void Deliver()
    {
        while (true)
        {
            if (_liveBlocks.TryDequeue(out LiveBlock block))
            {
                Interlocked.Add(ref _liveBytes, -block.Data.Length);
                UpdateStreamRoom();
                raise(block.Data.Length, block.Total, block.Data);
                continue;
            }

            long progressed = Interlocked.Exchange(ref _undelivered, 0);
            if (progressed > 0)
            {
                raise(progressed, Volatile.Read(ref _written), default);
                continue;
            }

            Volatile.Write(ref _delivering, 0);
            // A block written after the checks above, while this deliverer was still marked as running,
            // did not start a deliverer of its own: take it here.
            if (!HasWork || Interlocked.CompareExchange(ref _delivering, 1, 0) != 0)
                return;
        }
    }

    private void UpdateStreamRoom()
    {
        long limit = Volatile.Read(ref _liveLimit);
        if (limit > 0 && Interlocked.Read(ref _liveBytes) > limit)
        {
            _streamRoom.Pause();
            // The deliverer may have taken the blocks between the check and the pause.
            if (Interlocked.Read(ref _liveBytes) <= limit)
                _streamRoom.Resume();
        }
        else
        {
            _streamRoom.Resume();
        }
    }

    private readonly record struct LiveBlock(long Total, Memory<byte> Data);
}
