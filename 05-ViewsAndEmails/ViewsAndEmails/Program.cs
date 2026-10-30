using ViewsAndEmails;

var hub = new AnalyticsAndEmailJobs();

Task[] viewers = new Task[10];
for (int t = 0; t < viewers.Length; t++)
{
    viewers[t] = Task.Run(() =>
    {
        for (int i = 0; i < 5_000; i++)
            hub.RecordView("koshary-bowl");
    });
}

Task[] producers = new Task[20];
for (int i = 0; i < producers.Length; i++)
{
    int id = i + 1;
    producers[i] = Task.Run(() => hub.PlaceOrder(id));
}

Task w1 = Task.Run(hub.EmailWorkerAsync);
Task w2 = Task.Run(hub.EmailWorkerAsync);

await Task.WhenAll(viewers);
await Task.WhenAll(producers);

hub.ProducersDone = true;
await Task.WhenAll(w1, w2);

int viewExpected = 10 * 5_000;
int viewActual = hub.Views.TryGetValue("koshary-bowl", out int v) ? v : 0;
Console.WriteLine($"Views expected={viewExpected}, actual={viewActual}");
Console.WriteLine($"Email jobs placed=20, unique sent={hub.SentEmails.Distinct().Count()}, total sends={hub.SentEmails.Count}");
