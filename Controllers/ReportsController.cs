using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("reports")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) => _db = db;

    [HttpGet("orders.csv")]
    public async Task ExportOrders(CancellationToken ct)
    {
        Response.ContentType = "text/csv";
        Response.Headers.ContentDisposition = "attachment; filename=\"orders.csv\"";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync("OrderId,CustomerEmail,CreatedAt,PaymentStatus,Total");

        // Streaming 2.3s
        // var rows = _db.Orders
        //     .AsNoTracking()
        //     .OrderBy(o => o.Id)
        //     .Select(o => new
        //     {
        //         o.Id,
        //         o.Customer.Email,
        //         o.CreatedAt,
        //         o.PaymentStatus,
        //         // Total = o.Items.Sum(i => i.UnitPrice * i.Quantity)
        //     })
        //     .AsAsyncEnumerable();


        // Streaming with keyset batching
        var lastId = 0;

        while (true)
        {
            var batch = await _db.Orders
                .AsNoTracking()
                .Where(o => o.Id > lastId)
                .OrderBy(o => o.Id)
                .Take(5000)
                .Select(o => new
                {
                    o.Id,
                    o.Customer.Email,
                    o.CreatedAt,
                    o.PaymentStatus,
                    Total = o.Items.Sum(i => i.UnitPrice * i.Quantity)   // you can put it back
                })
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            foreach (var r in batch)
            {
                await writer.WriteLineAsync(
                    $"{r.Id},{Csv(r.Email)},{r.CreatedAt:O},{r.PaymentStatus},{r.Total}");
            }

            await writer.FlushAsync(ct);          // push this chunk to the client now
            lastId = batch[^1].Id;                // the "keyset": where the next chunk starts
        }

        // Load all rows in memory
        // var all = await _db.Orders
        //     .AsNoTracking()
        //     .OrderBy(o => o.Id)
        //     .Select(o => new
        //     {
        //         o.Id,
        //         o.Customer.Email,
        //         o.CreatedAt,
        //         o.PaymentStatus,
        //         // Total = o.Items.Sum(i => i.UnitPrice * i.Quantity)
        //     })
        //     .ToListAsync(ct);

        // Return result for streaming 2.3s
        // .WithCancellation(ct)
        // await foreach (var r in rows.WithCancellation(ct))
        // // foreach (var r in all)
        // {
        //     await writer.WriteLineAsync(
        //         // $"{r.Id},{Csv(r.Email)},{r.CreatedAt:O},{r.PaymentStatus},{r.Total}"
        //         $"{r.Id},{Csv(r.Email)},{r.CreatedAt:O},{r.PaymentStatus}"
        //     );
        // }

        // await writer.FlushAsync(ct);
    }

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}