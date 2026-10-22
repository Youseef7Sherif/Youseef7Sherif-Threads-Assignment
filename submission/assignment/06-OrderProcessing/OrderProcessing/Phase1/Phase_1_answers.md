## Phase 1 answers

**1. Why are the sync and async times almost the same when orders are awaited one after another?**
Because each order waits for the previous one to finish. `await` does not make a delay shorter; it only frees the thread while waiting. The total waiting time is the same sum of delays. In my run: sync <34.5> s, async (validation only) <35.8> s.

**2. Is the thread ID before an await always the same as after it?**
No. After an `await`, the code continues on any free thread from the thread pool. In my log:
`[12:35:45.245] [Thread 10] CustomerRepository -> Executing query for Customer #1...`
then
`[12:35:45.632] [Thread 11] CustomerRepository -> Customer #1 found (Ahmed Ali)`
The thread changed from 10 to 11. In the sync run, every line was on Thread 8.

**3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?**
With `Task.Delay`, the thread is released back to the thread pool and can run other work; a timer resumes the method later. With `Thread.Sleep`, the thread is blocked: it stays held and does nothing until the time is over. In my sync run, Thread 8 was blocked for the whole run.

**4. Why should a repository method return Task<T> instead of T?**
Because the result is not ready when the method returns. `Task<T>` represents work that will finish later, so the caller can `await` it without blocking a thread. Returning `T` would force the method to block until the value is ready.

**5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?**
`File.ReadAllText` blocks the calling thread until the whole file is read. `File.ReadAllTextAsync` returns a `Task` and the caller awaits it. When the file is opened for asynchronous I/O, the operating system does the read and no thread is held while the disk is working; the thread is only used again when the result is ready.
