# Assignment 03 — Answers

Student name:  Saeid Mohamed Elkhouly
Group / ID: Group#1

1. `System.Threading.Monitor` is a class that controls access to an object so that only one thread can enter a protected block at a time. It works by acquiring and releasing an exclusive lock on any reference-type object, and it also supports `Wait` / `Pulse` so threads can signal each other while inside the lock.

2. The C# `lock` statement compiles to a `try/finally` block that calls `Monitor.Enter` (with a `lockTaken` bool) before the body and `Monitor.Exit` inside the `finally`, ensuring the lock is always released even if an exception is thrown.

3.
   - `lock` always blocks until the lock is available and always releases it in the generated `finally`; with `Monitor` directly you can call `Monitor.TryEnter` to attempt acquisition with a timeout and skip the block if you don't get it.
   - `lock` has no way to make a thread wait for a condition and release the lock temporarily; `Monitor.Wait` and `Monitor.Pulse` let you do that (producer/consumer patterns), which is impossible to express with `lock` alone.

4.
   - `Monitor.TryEnter` — when you want to do something else instead of blocking indefinitely (e.g. skip a task if the resource is busy).
   - `Monitor.Wait` / `Monitor.Pulse` — when one thread must pause inside the lock and wait for another thread to signal that a condition is ready (classic producer/consumer queue).

5.
```csharp
// With lock
lock (_sync)
{
    _count++;
}

// With Monitor.Enter / Monitor.Exit
bool taken = false;
try
{
    Monitor.Enter(_sync, ref taken);
    _count++;
}
finally
{
    if (taken) Monitor.Exit(_sync);
}
```

Links:

- https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock