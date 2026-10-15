# Part 05 — Views and Emails

## 1. Failures before the fix (R1)

I ran the starter several times and got a different failure each time:

- Run 1: crash with `ArgumentException: Destination array was not long enough` inside `Queue.Enqueue` (called from `PlaceOrder`). Many threads added jobs to the same `Queue` at once and broke its internal array while it was growing.
- Run 2: crash with `ArgumentException: An item with the same key has already been added. Key: koshary-bowl` in `RecordView`, then `InvalidOperationException: ... corrupted its state`. Two threads checked `ContainsKey`, both saw the product was missing, and both called `Add`. The `Dictionary` was then corrupted by concurrent changes.
- Run 3: no exception, but `Views expected=50000, actual=14291`. Increments were lost, because `Views[product] + 1` is a read, add and write that threads overlap. The emails were correct in this run (`placed=20, unique sent=20, total sends=20`).

The result changes in every run because it depends on how the threads are scheduled.

## 2. The fix (R2)

I protected each shared collection with its own `lock`:

- `RecordView`: the lookup (`TryGetValue`), the +1 and the write happen inside one `lock`.
- `PlaceOrder`: `Enqueue` is inside a `lock`.
- `EmailWorker`: `TryDequeue` checks and takes a job in one step inside the `lock`, so two workers never take the same job and nobody dequeues from an empty queue. I removed the `catch (InvalidOperationException)`, because it only hid the error.
- To stop safely, the worker reads `ProducersDone` before trying to take a job. If it was already true and the queue is empty, no job can arrive later, so the worker returns.
- `ProducersDone` is `volatile` so the workers always read its latest value, written by the main thread.

## 3. After the fix

| Run | Views (expected 50000) | Emails (placed / unique / total) | Exceptions |
| --- | ---------------------- | -------------------------------- | ---------- |
| 1   | 50000                  | 20 / 20 / 20                     | none       |
| 2   | 50000                  | 20 / 20 / 20                     | none       |
| 3   | 50000                  | 20 / 20 / 20                     | none       |
| 4   | 50000                  | 20 / 20 / 20                     | none       |
| 5   | 50000                  | 20 / 20 / 20                     | none       |

## 4. Why a safer collection is not enough (R3)

A thread-safe collection only protects each single operation. A business rule often has several steps, and another thread can run between them. In the original code, `ContainsKey` then `Add` then `+ 1` were separate steps, and `Count > 0` then `Dequeue` were separate steps. Even with `ConcurrentDictionary`, writing `if (!ContainsKey(x)) ...` and then updating would still have a gap between the two calls. The whole rule must be one atomic step: a single method like `AddOrUpdate` or `TryDequeue`, or one `lock` around all the steps.
