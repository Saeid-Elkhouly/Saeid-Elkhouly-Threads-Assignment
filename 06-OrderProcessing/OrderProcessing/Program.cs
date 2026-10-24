
using OrderProcessingSystem.Phase1;
using OrderProcessingSystem.Phase2;
using OrderProcessingSystem.Shared.Storage;

namespace OrderProcessingSystem;

internal static class Program
{
    private static async Task Main()
    {
        JsonFileStore store = new();
       // Phase1Runner runner = new(store);

       // await runner.RunAsync(CancellationToken.None);
          Phase2Runner runner = new(store);

        await runner.RunAsync(CancellationToken.None);
    }
}
