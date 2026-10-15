# Part 04 — Menu Views

## 1. Before the fix (R1)

8 threads each call `RegisterView()` 50,000 times on one shared counter. Expected = 8 × 50,000 = 400,000.

| Run | Expected | Actual |
| --- | -------- | ------ |
| 1   | 400000   | 133857 |
| 2   | 400000   | 139434 |
| 3   | 400000   | 115238 |

## 2. Why the numbers disagree (R2)

`Views++` looks like one step, but it is three: read the value, add 1, write the value back. When two threads read the same value before either one writes, both write the same result, and one increment is lost. With 8 threads doing 400,000 increments, this happens very often, so about two thirds of the views were lost. The result also changes in every run because it depends on how the threads are scheduled.

## 3. The fix (R3)

I used `Interlocked.Increment`:

```csharp
public void RegisterView()
{
    Interlocked.Increment(ref Views);
}
```

It does the read, add and write as one atomic operation at the CPU level, so no increment can be lost. For one counter this is the right tool: it is faster than `lock`, because threads do not go to sleep and wake up again.

## 4. After the fix

| Run | Expected | Actual |
| --- | -------- | ------ |
| 1   | 400000   | 400000 |
| 2   | 400000   | 400000 |
| 3   | 400000   | 400000 |

## 5. When to protect it differently (R4)

`Interlocked` only protects one simple operation on one variable. If the update needs more than that, I would use `lock`. For example, in Part 02 the code checks `TablesLeft`, then changes two variables (`TablesLeft` and `Booked`). Those steps must happen together, so one `lock` has to cover all of them.
