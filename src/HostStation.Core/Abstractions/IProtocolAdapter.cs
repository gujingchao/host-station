namespace HostStation.Core.Abstractions;

public interface IProtocolAdapter
{
    string Name { get; }
    Task<IReadOnlyList<TagSample>> PollAsync(CancellationToken cancellationToken = default);
}

public sealed record TagSample(string Tag, double Value, DateTimeOffset Timestamp, string? Quality = null);
