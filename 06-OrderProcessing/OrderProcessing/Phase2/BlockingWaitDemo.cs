namespace OrderProcessingSystem.Phase2;

// This is the only Phase 2 method that deliberately blocks on an unfinished Task.
public static class BlockingWaitDemo
{
    public static void Run()
    {
        Task<int> task = GetValueAsync();
        Console.WriteLine("The task has started; the calling thread now blocks on Result.");
        int result = task.Result;
        Console.WriteLine($"Result: {result}");
    }

    private static async Task<int> GetValueAsync()
    {
        await Task.Delay(500);
        return 42;
    }
}
