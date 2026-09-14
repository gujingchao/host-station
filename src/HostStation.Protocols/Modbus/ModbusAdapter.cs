using HostStation.Core.Abstractions;

namespace HostStation.Protocols.Modbus;

public enum ModbusMode
{
    Tcp,
    Rtu,
}

/// <summary>Modbus poll adapter stub — returns synthetic registers for tests.</summary>
public sealed class ModbusAdapter : IProtocolAdapter
{
    private readonly Func<IReadOnlyDictionary<string, double>> _source;

    public ModbusAdapter(string name, ModbusMode mode, Func<IReadOnlyDictionary<string, double>>? source = null)
    {
        Name = name;
        Mode = mode;
        _source = source ?? (() => new Dictionary<string, double>
        {
            ["HR40001"] = 1,
            ["HR40002"] = 2,
        });
    }

    public string Name { get; }
    public ModbusMode Mode { get; }

    public Task<IReadOnlyList<TagSample>> PollAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<TagSample> samples = _source()
            .Select(kv => new TagSample(kv.Key, kv.Value, now, "Good"))
            .ToList();
        return Task.FromResult(samples);
    }
}
