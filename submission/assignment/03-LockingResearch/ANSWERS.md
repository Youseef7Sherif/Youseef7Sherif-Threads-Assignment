# Part 03 — Locking Research

## Q1. What is Monitor used for?

Monitor makes threads take turns. Only one thread can be inside the protected code at a time, and the others wait. It can also let one thread tell another thread "you can continue now" (Wait and Pulse).

## Q2. What does lock compile to?

`lock` is a short way to write `Monitor.Enter` and `Monitor.Exit` inside a `try/finally`. The `finally` gives the lock back even if an error happens.

```csharp
bool lockTaken = false;
try
{
    Monitor.Enter(obj, ref lockTaken);
    // protected code
}
finally
{
    if (lockTaken) Monitor.Exit(obj);
}
```

## Q3. Differences between lock and Monitor

1. `lock` gives the lock back by itself. With Monitor, I must do it myself. If I forget, other threads wait forever.
2. `lock` waits as long as needed. `Monitor.TryEnter` can stop waiting after some time.
3. Only Monitor has Wait and Pulse.

## Q4. When to use TryEnter, Wait or Pulse

- TryEnter: when I do not want a thread to wait forever. If the lock is busy, it can skip the work or try later.
- Wait and Pulse: when a thread must wait for something to happen. Example: a consumer waits while the queue is empty, and the producer calls Pulse after adding an item.

## Q5. The same code twice (from Part 02)

```csharp
// with lock
lock (_lock)
{
    if (TablesLeft <= 0) return;
    TablesLeft--;
    Booked++;
}

// with Monitor
bool lockTaken = false;
try
{
    Monitor.Enter(_lock, ref lockTaken);
    if (TablesLeft <= 0) return;
    TablesLeft--;
    Booked++;
}
finally
{
    if (lockTaken) Monitor.Exit(_lock);
}
```

## Links

- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor
