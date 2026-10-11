using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

public interface IOrderService
{
    Task<(Order? Order, string? Error)> CreateAsync(int customerId, CreateOrderRequest requst);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) => _db = db;

    public async Task<(Order? Order, string? Error)> CreateAsync(int customerId, CreateOrderRequest request)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return (null, "Customer not found");

        var ids = request.Items.Select(i => i.ProductId).Distinct().ToList();
        // [1, 4, 10]
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        if (products.Count != ids.Count) return (null, "Requested product not found");

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
        return (order, null);
    }
}