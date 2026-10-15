# Part 02 — Table Booking

**Student name:** <Youssef_Mohamed_Sherif>
**Group / ID:** <group_2>

## 1. Original behaviour (R1)

40 customer threads call `Book` on one shared `FridayNightBooking` object that starts with 5 tables. I ran the starter 5 times:

| Run | Lines "booked a table" | Booked | TablesLeft |
| --- | ---------------------- | ------ | ---------- |
| 1   | 40                     | 35     | 4          |
| 2   | 40                     | 36     | 4          |
| 3   | 40                     | 39     | 4          |
| 4   | 40                     | 31     | 4          |
| 5   | 40                     | 14     | 4          |

Example of the output (first lines and the last line of run 1):

```
Customer-31 booked a table. Left=4
Customer-32 booked a table. Left=4
Customer-35 booked a table. Left=4
...
Booked=35, TablesLeft=4
```

## 2. What is wrong and why (R2)

1. **Check-then-act race.** `Book` checks `TablesLeft <= 0`, then sleeps 20 ms, and only then decrements. All 40 threads reach the check before any of them decrements, so all of them see 5 tables and pass. 40 customers were served for 5 tables (overbooking).
2. **`TablesLeft--` and `Booked++` are not atomic.** Each one is read, calculate, write. When two threads read the same value before either writes, both write the same result and one update is lost. This is why `TablesLeft` stayed at 4 and `Booked` was different in every run (14 to 39) even though 40 customers booked.
3. **The numbers change between runs** because they depend on how the operating system schedules the threads. I did not try to predict the exact values; the point is that a correct program must give the same result every time.

## 3. The fix (R3, R4)

I put the check and both updates inside one `lock`, so they happen as one unit and only one thread can be inside at a time:

```csharp
readonly object _lock = new();

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

    Thread.Sleep(20);   // simulated work, outside the lock
    Console.WriteLine($"{customerName} booked a table. Left={left}");
}
```

Why it works:

- A thread that gets the lock sees the real current value, and nobody can change it until the thread leaves. So only the first 5 threads pass the check, and the others return.
- `Interlocked.Decrement` alone would not be enough here, because the rule involves a check plus two variables that must change together.
- The `Thread.Sleep` is outside the lock so the threads do not wait for each other 20 ms each. The lock is held only for a few fast lines.
- `left` is saved inside the lock and printed after it, so the printed number is the one this thread produced, not a value changed later by another thread.

## 4. Output after the fix

I ran it 5 times:

| Run | Lines "booked a table" | Booked | TablesLeft |
| --- | ---------------------- | ------ | ---------- |
| 1   | 5                      | 5      | 0          |
| 2   | 5                      | 5      | 0          |
| 3   | 5                      | 5      | 0          |
| 4   | 5                      | 5      | 0          |
| 5   | 5                      | 5      | 0          |

Output of run 1:

```
Customer-03 booked a table. Left=1
Customer-05 booked a table. Left=0
Customer-01 booked a table. Left=4
Customer-02 booked a table. Left=2
Customer-04 booked a table. Left=3

Booked=5, TablesLeft=0
```

Every run gives exactly 5 bookings, `Left` takes each value from 4 down to 0 exactly once, and the final line is always `Booked=5, TablesLeft=0`. The order of the lines changes between runs because the printing happens after the `Thread.Sleep`, outside the lock, so it depends on which thread wakes up first. In my runs the five customers who got a table were always Customer-01 to Customer-05, since they are the first threads started. The order of the lines is not a problem; the values and the totals are what matter.
