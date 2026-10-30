# Assignment 01 — Answers

Student name:  Saeid Mohamed Elkhouly
Group / ID: Group#1

## Problems with the current design

- All I/O calls use `Thread.Sleep` and synchronous `File.*` methods, blocking the thread for the full duration of each operation.
- The `foreach` loop processes orders one at a time. Orders are independent and could run concurrently.
- `GetOrderById` is called once per order inside the loop, re-fetching data already returned by `GetPendingOrders`.
- `ExportAllPending` is `void`, so it cannot be awaited and one failed order crashes the entire batch.

## What you changed

- Replaced every `Thread.Sleep` with `await Task.Delay` and every synchronous `File.*` call with its `Async` equivalent.
- Changed all methods to `async Task` (or `async Task<T>`).
- Replaced the `foreach` with `await Task.WhenAll(orders.Select(async order => ...))` so all orders run concurrently.
- Removed the redundant `GetOrderById` call inside the loop; the order object from `GetPendingOrdersAsync` is used directly.
- Added a `try/catch` inside each task lambda so one failing order is logged without stopping the rest.