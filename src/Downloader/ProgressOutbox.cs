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
    private volatile bool _liveStreaming; // set by Start
    private long _liveLimit; // set by Start
    private long _liveBytes; // bytes of the blocks in _liveBlocks
    private long _written; // bytes on the storage; only the writer thread changes it while a download runs
    private long _reported; // normal mode: the last total the handler got
    private int _delivering; // 1 while a deliverer runs

    /// <summary>
    /// Paused while live-streaming blocks wait for the handler beyond the memory limit.
    /// A chunk waits on it before it reads the next block from the network.
    /// </summary>
    public PauseToken StreamRoom => _streamRoom.Token;

    /// <summary>
    /// True when every update so far has reached the handler.
    /// </summary>
    // Work first, then the flag: a deliverer takes work while the flag is set and clears it only after the
    // handler returned. The other order can see the flag clear for a moment before the deliverer goes on.
    public bool IsIdle => !HasWork && Volatile.Read(ref _delivering) == 0;

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
        Interlocked.Exchange(ref _reported, writtenBytes);
        UpdateStreamRoom();
    }

    /// <summary>
    /// The bytes on the storage, as the writer reported them so far.
    /// </summary>
    public long Written => Volatile.Read(ref _written);

    /// <summary>
    /// Called by the storage writer after a data block is written. Never waits.
    /// </summary>
    /// <param name="length">The length of the block.</param>
    /// <param name="liveData">A copy of the block for live streaming, or empty.</param>
    public void OnWritten(int length, Memory<byte> liveData)
    {
        long written = Volatile.Read(ref _written) + length;
        Volatile.Write(ref _written, written);
        if (!hasHandler())
        {
            // Nobody listens: a handler added later starts from here, not with every byte so far.
            Interlocked.Exchange(ref _reported, written);
            return;
        }

        if (_liveStreaming)
        {
            _liveBlocks.Enqueue(new LiveBlock(written, liveData));
            Interlocked.Add(ref _liveBytes, length);
            UpdateStreamRoom();
        }

        TryStartDeliverer();
    }

    /// <summary>
    /// Drops everything the handler did not take yet, and lets a waiting chunk go on.
    /// </summary>
    public void DropUndelivered()
    {
        while (_liveBlocks.TryDequeue(out LiveBlock block))
            Interlocked.Add(ref _liveBytes, -block.Data.Length);

        Interlocked.Exchange(ref _reported, Volatile.Read(ref _written));
        _streamRoom.Resume();
    }

    private bool HasWork => !_liveBlocks.IsEmpty ||
                            (!_liveStreaming && Volatile.Read(ref _written) > Interlocked.Read(ref _reported));

    private void TryStartDeliverer()
    {
        if (Interlocked.CompareExchange(ref _delivering, 1, 0) == 0)
            ThreadPool.QueueUserWorkItem(static outbox => outbox.Deliver(), this, preferLocal: false);
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

            if (!_liveStreaming && TryTakeNewBytes(out long progressed, out long total))
            {
                raise(progressed, total, default);
                continue;
            }

            // A full fence (not a plain write): the work check below must not read values older than this
            // release, or a block written right now would start no deliverer and nobody would take it.
            Interlocked.Exchange(ref _delivering, 0);
            if (!HasWork || Interlocked.CompareExchange(ref _delivering, 1, 0) != 0)
                return;
        }
    }

    /// <summary>
    /// Takes the bytes written since the last total the handler got. The total and the bytes come from the
    /// same read, so the newest total is never lost and the totals always add up.
    /// </summary>
    private bool TryTakeNewBytes(out long progressed, out long total)
    {
        long reported = Interlocked.Read(ref _reported);
        total = Volatile.Read(ref _written);
        progressed = total - reported;
        // Start may move _reported meanwhile (a new run): then take nothing, and look again.
        return progressed > 0 && Interlocked.CompareExchange(ref _reported, total, reported) == reported;
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
