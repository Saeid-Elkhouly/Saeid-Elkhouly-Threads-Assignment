using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync()
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders = await _db.GetPendingOrdersAsync();

        await Task.WhenAll(orders.Select(async order =>
        {
            try
            {
                CustomerProfile customer = await _customers.ReadProfileAsync(order.CustomerId);
                string path = await _invoices.WriteInvoiceAsync(order, customer);
                Console.WriteLine($"Wrote {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed order {order.Id}: {ex.Message}");
            }
        }));

        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}
