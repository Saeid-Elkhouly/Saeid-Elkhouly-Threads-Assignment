using System.Collections.Concurrent;
using OrderProcessingSystem.Shared.Exceptions;
using OrderProcessingSystem.Shared.Models;
using OrderProcessingSystem.Shared.Services;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem.Phase3;

public sealed class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(JsonFileStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _orderRepository = new OrderRepository(store);
        _emailService = new EmailService(store);
    }

    private FireAndForgetDemo(JsonFileStore store, IEmailService emailService)
    {
        _orderRepository = new OrderRepository(store);
        _emailService = emailService;
    }

    public async Task ProcessOrderAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);
#pragma warning disable CS4014
        _emailService.SendConfirmationAsync(order, customer, CancellationToken.None);   // not awaited
#pragma warning restore CS4014
        Console.WriteLine("Order processing finished");
    }

    public async Task ProcessOrderFixedAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);
        await _emailService.SendConfirmationAsync(order, customer, CancellationToken.None);
        Console.WriteLine("Order processing finished");
    }

    public static async Task RunControlledScenariosAsync(JsonFileStore store, Order order, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(customer);

        ControlledEmailService emailService = new();
        FireAndForgetDemo demo = new(store, emailService);

        Console.WriteLine("Broken method: email remains pending after the method returns.");
        TaskCompletionSource emailStarted = emailService.PrepareNextAttempt();
        Task brokenEmailTask = emailService.NextAttemptTask;
        await demo.ProcessOrderAsync(order, customer);
        await emailStarted.Task;
        Console.WriteLine("Observed: ProcessOrderAsync returned while email was still pending.");
        emailService.FailNextAttempt();
        try
        {
            await brokenEmailTask;
        }
        catch (SmtpUnavailableException ex)
        {
            Console.WriteLine($"Harness observed the later email failure: {ex.Message}");
        }
        Console.WriteLine("The broken processing method did not observe that email exception.");

        Console.WriteLine("\nFixed method: caller waits for successful email completion.");
        emailStarted = emailService.PrepareNextAttempt();
        Task fixedSuccess = demo.ProcessOrderFixedAsync(order, customer);
        await emailStarted.Task;
        Console.WriteLine("Observed: ProcessOrderFixedAsync is still pending while email is pending.");
        emailService.CompleteNextAttempt();
        await fixedSuccess;

        Console.WriteLine("\nFixed method: caller observes an email failure.");
        emailStarted = emailService.PrepareNextAttempt();
        Task fixedFailure = demo.ProcessOrderFixedAsync(order, customer);
        await emailStarted.Task;
        emailService.FailNextAttempt();
        try
        {
            await fixedFailure;
        }
        catch (SmtpUnavailableException ex)
        {
            Console.WriteLine($"Awaited caller observed the email failure: {ex.Message}");
        }
    }

    private sealed class ControlledEmailService : IEmailService
    {
        private readonly ConcurrentQueue<Attempt> _attempts = new();
        private Attempt? _current;

        public Task NextAttemptTask => _current?.Completion.Task
            ?? throw new InvalidOperationException("No email attempt has been prepared.");

        public TaskCompletionSource PrepareNextAttempt()
        {
            Attempt attempt = new();
            _current = attempt;
            _attempts.Enqueue(attempt);
            return attempt.Started;
        }

        public Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
        {
            if (!_attempts.TryDequeue(out Attempt? attempt))
            {
                throw new InvalidOperationException("No controlled email attempt is available.");
            }

            attempt.Started.TrySetResult();
            return attempt.Completion.Task.WaitAsync(ct);
        }

        public void CompleteNextAttempt() => Current.Completion.TrySetResult();

        public void FailNextAttempt() =>
            Current.Completion.TrySetException(new SmtpUnavailableException("Controlled email failure"));

        private Attempt Current => _current
            ?? throw new InvalidOperationException("No email attempt has been prepared.");

        private sealed class Attempt
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
