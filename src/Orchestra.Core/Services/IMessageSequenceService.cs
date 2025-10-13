namespace Orchestra.Core.Services;

/// <summary>
/// Service for generating monotonically increasing sequence numbers for messages
/// to ensure correct ordering in concurrent environments
/// </summary>
public interface IMessageSequenceService
{
    /// <summary>
    /// Gets the next sequence number in a thread-safe manner
    /// </summary>
    /// <returns>Unique sequence number</returns>
    long GetNextSequence();
}
