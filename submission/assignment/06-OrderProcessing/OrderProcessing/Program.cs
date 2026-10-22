using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
namespace OrderProcessing;

public static class Program
{
    static async Task Main(string[] args)
    {
        string? mode = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant();
        int? cancelAfterMs = ParseCancelAfter(args);

        mode ??= ShowMenu();

        switch (mode)
        {
            case "phase1":
                await RunPhaseAsync(Phase1Runner.RunAsync, cancelAfterMs);
                break;
            case "phase2":
                await RunPhaseAsync(Phase2Runner.RunAsync, cancelAfterMs);
                break;
            case "phase3":
            case "phase4":
                Console.WriteLine($"{mode} is not built yet.");
                break;
            case "all":
                await RunPhaseAsync(Phase1Runner.RunAsync, cancelAfterMs);
                await RunPhaseAsync(Phase2Runner.RunAsync, cancelAfterMs);
                break;
            default:
                Console.WriteLine("Usage: dotnet run -- phase1|phase2|phase3|phase4|all [--cancel-after <ms>]");
                break;
        }
    }

    // Each phase run owns ONE cancellation source
    static async Task RunPhaseAsync(Func<CancellationToken, Task> run, int? cancelAfterMs)
    {
        using var cts = new CancellationTokenSource();

        ConsoleCancelEventHandler onCtrlC = (_, e) =>
        {
            e.Cancel = true;   // do not kill the process, cancel the run instead
            cts.Cancel();
        };
        Console.CancelKeyPress += onCtrlC;

        try
        {
            if (cancelAfterMs is { } ms)
                cts.CancelAfter(ms);

            await run(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Run cancelled.");
        }
        finally
        {
            Console.CancelKeyPress -= onCtrlC;
        }
    }

    static int? ParseCancelAfter(string[] args)
    {
        int index = Array.IndexOf(args, "--cancel-after");
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int ms))
            return ms;
        return null;
    }

    static string ShowMenu()
    {
        Console.WriteLine("===== Order Processing System =====");
        Console.WriteLine("1) Phase 1 - Async data access layer");
        Console.WriteLine("2) Phase 2 - Concurrent processing");
        Console.WriteLine("3) Phase 3 - Full checkout workflow");
        Console.WriteLine("4) Phase 4 - Scale-up");
        Console.WriteLine("5) All phases");
        Console.Write("Choose: ");

        return Console.ReadLine()?.Trim() switch
        {
            "1" => "phase1",
            "2" => "phase2",
            "3" => "phase3",
            "4" => "phase4",
            "5" => "all",
            _ => ""
        };
    }
}