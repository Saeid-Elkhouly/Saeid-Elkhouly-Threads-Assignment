using System.Collections.Concurrent;

namespace ViewsAndEmails;

public sealed class AnalyticsAndEmailJobs
{
    public readonly ConcurrentDictionary<string, int> Views = new();
    public readonly ConcurrentQueue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    readonly object _sentSync = new();
    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        Views.AddOrUpdate(product, 1, (_, current) => current + 1);
    }

    public void PlaceOrder(int orderId)
    {
        EmailJobs.Enqueue($"Email: order {orderId}");
    }

    public async Task EmailWorkerAsync()
    {
        while (true)
        {
            if (EmailJobs.TryDequeue(out string? job))
            {
                lock (_sentSync)
                    SentEmails.Add(job);
                continue;
            }

            if (ProducersDone && EmailJobs.IsEmpty)
                return;

            await Task.Delay(1000);
        }
    }
}
