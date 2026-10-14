using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
public class OrderLineRequest
{
    public int ProductId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; }
}

public class CreateOrderRequest
{   
    [Required, MinLength(1)] public List<OrderLineRequest> Items { get; set; } = new();
}

public class OrderItemResponse
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderResponse
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public  List<OrderItemResponse> Items { get; set; } = new();
}

[ApiController]
[Authorize]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly AppDbContext _db;
    private readonly IPaymentService _payments;
    public OrdersController(AppDbContext db, IOrderService orders, IPaymentService payment)
    {
        _db = db;
        _orders = orders;
        _payments = payment;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponse>> GetOne(int id)
    {
        var isAdmin = User.IsInRole("Admin");
        int.TryParse(User.FindFirstValue("customerId"), out var customerId);

        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id && (isAdmin || o.CustomerId == customerId));

        if (order is null) return NotFound();

        var response = new OrderResponse
        {
            Id = order.Id,
            CustomerName = order.Customer.Name,
            Items = order.Items.Select(i => new OrderItemResponse
            {
                ProductName = i.Product.Name,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult> Create(CreateOrderRequest request)
    {
        if (!int.TryParse(User.FindFirstValue("customerId"), out var customerId))
            return Forbid();
        var (order, error) = await _orders.CreateAsync(customerId, request);
        if (error is not null) return BadRequest(error);
        return CreatedAtAction(nameof(GetOne), new { id = order!.Id}, new { order.Id });
    }

    [HttpPost("{id}/pay")]
    public async Task<ActionResult> Pay(int id, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        int.TryParse(User.FindFirstValue("customerId"), out var customerId);

        var (url, error) = await _payments.StartPaymentAsync(id, customerId, isAdmin, ct);

        if (error == "Order not found") return NotFound();
        if (error is not null) return Conflict(error);
        return Ok(new { paymentUrl = url });
    }
}


