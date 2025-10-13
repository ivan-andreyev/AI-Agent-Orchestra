namespace Orchestra.Core.Services;

/// <summary>
/// Thread-safe implementation of message sequence number generator
/// Uses Interlocked operations to ensure atomicity in concurrent environments
/// </summary>
public class MessageSequenceService : IMessageSequenceService
{
    private long _currentSequence = 0;

    /// <summary>
    /// Gets the next sequence number in a thread-safe manner using Interlocked.Increment
    /// </summary>
    /// <returns>Unique monotonically increasing sequence number</returns>
    public long GetNextSequence()
    {
        return System.Threading.Interlocked.Increment(ref _currentSequence);
    }
}
