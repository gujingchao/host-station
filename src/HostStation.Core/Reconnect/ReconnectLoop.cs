using HostStation.Core.Abstractions;

namespace HostStation.Core.Reconnect;

public static class ReconnectLoop
{
    public static async Task RunAsync(
        IDeviceTransport transport,
        ReconnectPolicy policy,
        CancellationToken cancellationToken = default)
    {
        foreach (var delay in policy.Delays())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"Failed to reconnect {transport.DeviceId} within policy limits.");
    }
}
