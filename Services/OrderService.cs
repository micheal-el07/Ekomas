using Microsoft.EntityFrameworkCore;

public interface IOrderService
{
    Task<(Order? Order, string? Error)> CreateAsync(int customerId, CreateOrderRequest request);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) => _db = db;

    public async Task<(Order? Order, string? Error)> CreateAsync(int customerId, CreateOrderRequest request)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return (null, "Customer not found");

        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != ids.Count) return (null, "Requested product not found");

        foreach (var line in request.Items)
        {
            var rows = await _db.Products
                .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity));

            if (rows == 0)
                return (null, $"Product {line.ProductId} is out of stock.");
        }

        var order = new Order { CustomerId = customer.Id };
        foreach (var line in request.Items)
        {
            order.Items.Add(new OrderItem
            {
               ProductId = line.ProductId,
               Quantity = line.Quantity,
               UnitPrice = products[line.ProductId].Price 
            });
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return (order, null);
    }
}