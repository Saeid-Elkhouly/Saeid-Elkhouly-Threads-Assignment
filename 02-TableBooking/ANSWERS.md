# Assignment 02 — Answers

Student name:  Saeid Mohamed Elkhouly
Group / ID: Group#1

## What is wrong

- `Book` has a race condition: multiple threads read `TablesLeft > 0` at the same time, all pass the check, then all decrement — so more than 5 tables get booked.
- The check and the decrement are not atomic; the `Thread.Sleep(20)` in between makes the window large enough for all 40 threads to slip through together.

## How you fixed it

- Added a ` readonly Lock _sync = new();` field to `FridayNightBooking`.
- Wrapped the entire body of `Book` in `lock (_sync)` so only one thread at a time can check `TablesLeft` and update it.
