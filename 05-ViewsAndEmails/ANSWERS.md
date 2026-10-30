# Assignment 05 — Answers

Student name:  Saeid Mohamed Elkhouly
Group / ID: Group#1

## Failures you observed

- `RecordView` crashes with exceptions such as `ArgumentException: An item with the same key has already been added`, `KeyNotFoundException: The given key was not present in the dictionary`, and `InvalidOperationException: Operations that change non-concurrent collections must have exclusive access`.
- When `RecordView` did not throw an exception, it lost views due to a race condition in `Views[product] = Views[product] + 1` (e.g. reporting ~20,000 views instead of the expected 50,000).
- `EmailJobs.Dequeue()` throws `InvalidOperationException: Queue empty` when multiple workers see `Count > 0` and attempt to dequeue simultaneously.
- Email jobs are dropped or lost when concurrent enqueues/dequeues corrupt the internal state of the non-thread-safe `Queue<T>` (e.g. unique sent = 19 instead of 20).

## What you changed

- Replaced `Dictionary<string, int>` with `ConcurrentDictionary<string, int>` and used `AddOrUpdate(product, 1, (_, current) => current + 1)` in `RecordView` for atomic, thread-safe counter increments.
- Replaced `Queue<string>` with `ConcurrentQueue<string>` and used atomic `Enqueue` in `PlaceOrder`.
- Converted worker and orchestrations to asynchronous tasks: `EmailWorker` became `async Task EmailWorkerAsync()` using `await Task.Delay(1)` instead of `Thread.Sleep(1)`.
- Replaced OS `Thread` instances in `Program.cs` with `Task.Run` and non-blocking `await Task.WhenAll(...)`.
- Kept `_sentSync` lock around `SentEmails.Add(job)` since `List<string>` is non-concurrent.

## Collection safety vs business-rule safety

- Using a thread-safe or concurrent collection (like `ConcurrentDictionary` or `ConcurrentQueue`) only guarantees that individual low-level operations (like `Add` or `TryDequeue`) won't corrupt memory or throw internal collection exceptions.
- It does **not** protect composite operations or business rules across multiple steps (check-then-act sequences). For example, checking `ContainsKey` followed by `Add` or reading a count followed by updating/incrementing still contains a race condition unless the entire business transaction is synchronized as a single atomic unit.