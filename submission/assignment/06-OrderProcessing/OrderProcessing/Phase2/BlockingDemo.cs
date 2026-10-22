using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase2;

// The ONLY place in the project where .Result is allowed (Phase 2, question 3)
public static class BlockingDemo
{
    public static void Run()
    {
        Log.Info("BlockingDemo -> calling .Result on a task that is not finished yet...");

        Task<int> task = SlowAsync();
        int value = task.Result;   // blocks THIS thread until the task finishes

        Log.Info($"BlockingDemo -> got {value}, but this thread did nothing for 1 second");
    }

    static async Task<int> SlowAsync()
    {
        await Task.Delay(1000);
        return 42;
    }
}