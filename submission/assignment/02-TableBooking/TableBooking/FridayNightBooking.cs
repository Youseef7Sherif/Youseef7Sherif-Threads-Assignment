namespace TableBooking;

public sealed class FridayNightBooking
{
    readonly object _lock = new();

    public int TablesLeft { get; private set; } = 5;
    public int Booked { get; private set; }

    public void Book(string customerName)
    {
        int left;

        lock (_lock)
        {
            if (TablesLeft <= 0)
                return;

            TablesLeft--;
            Booked++;
            left = TablesLeft;
        }

        Thread.Sleep(20);   // simulated work, done outside the lock
        Console.WriteLine($"{customerName} booked a table. Left={left}");
    }
}