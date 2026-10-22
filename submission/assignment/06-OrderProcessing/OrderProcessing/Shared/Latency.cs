using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Shared;

public static class Latency
{
    public static int Between(int min, int max) => Random.Shared.Next(min, max + 1);

    public static async Task StepAsync(string source, string message, int milliseconds, CancellationToken ct)
    {
        Log.Info($"{source} -> {message}");
        await Task.Delay(milliseconds, ct);
    }

    public static void Step(string source, string message, int milliseconds)
    {
        Log.Info($"{source} -> {message}");
        Thread.Sleep(milliseconds);
    }
}