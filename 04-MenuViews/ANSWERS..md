# Assignment 04 — Answers

Student name:  Saeid Mohamed Elkhouly
Group / ID: Group#1

## Why Expected and Actual disagree

- `Views++` is not an atomic operation; it consists of three separate steps: read the current value into a CPU register, increment the value, and write it back to memory.
- When multiple threads execute `Views++` concurrently without synchronization, they read the same stale value and overwrite each other's updates, resulting in lost updates.

## What you changed

- Replaced `Views++` with `Interlocked.Increment(ref Views)`.
- This executes the read-modify-write as a single atomic CPU instruction (e.g. `LOCK INC`) without the overhead of locks.

## When would you protect the update differently

- When multiple variables or state must be kept consistent together (e.g. updating a counter alongside updating another field or list), use a `lock` or `Monitor`.
- When there are complex conditions (e.g. "increment only if below a threshold, otherwise do X"), use a `lock`, `SpinLock`, or a compare-exchange loop (`Interlocked.CompareExchange`).
- In high-contention read-heavy scenarios, `ReaderWriterLockSlim` or per-thread counters (aggregating at the end) would be preferred to reduce cache-line bouncing.