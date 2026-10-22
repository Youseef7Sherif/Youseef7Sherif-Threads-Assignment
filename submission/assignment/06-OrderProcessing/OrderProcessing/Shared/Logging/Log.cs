namespace OrderProcessing.Shared.Logging;

public static class Log
{
    static readonly object Sync = new();

    public static void Info(string message) => Write(message, null);
    public static void Success(string message) => Write(message, ConsoleColor.Green);
    public static void Error(string message) => Write(message, ConsoleColor.Red);

    static void Write(string message, ConsoleColor? color)
    {
        // The thread id is read here, on the thread that called Log
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] {message}";

        lock (Sync)
        {
            if (color is { } c)
                Console.ForegroundColor = c;

            Console.WriteLine(line);

            if (color is not null)
                Console.ResetColor();
        }
    }
}