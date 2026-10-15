using Microsoft.EntityFrameworkCore;

public class ExpiredOrderSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(3);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ExpiredOrderSweeper> _logger;

    public ExpiredOrderSweeper(IServiceScopeFactory scopes, ILogger<ExpiredOrderSweeper> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Order sweep failed");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow - MaxAge;

        var staleIds = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt < cutoff &&
                    (o.PaymentStatus == PaymentStatus.Unpaid || o.PaymentStatus == PaymentStatus.Pending))
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (var id in staleIds)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var claimed = await db.Orders
                .Where(o => o.Id == id &&
                            (o.PaymentStatus == PaymentStatus.Unpaid || o.PaymentStatus == PaymentStatus.Pending))
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentStatus, PaymentStatus.Expired), ct);

            if (claimed == 0) continue;

            var items = await db.OrderItems.AsNoTracking()
                .Where(i => i.OrderId == id)
                .Select(i => new { i.ProductId, i.Quantity})
                .ToListAsync(ct);

            foreach (var item in items)
            {
                await db.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity), ct); 
            }

            await tx.CommitAsync(ct);
            _logger.LogInformation("Expired order {OrderId} and restocked {Lines} lines", id, items.Count);
        }
    }
}