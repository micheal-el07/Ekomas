using Microsoft.EntityFrameworkCore;

public interface IPaymentService
{
    Task<(string? PaymentUrl, string? Error)> StartPaymentAsync(
        int orderId, int customerId, bool isAdmin, CancellationToken ct
    );

    Task ApplyCallbackAsync (
        XenditInvoiceCallback payload,
        CancellationToken ct
    );
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(AppDbContext db, IPaymentGateway gateway, ILogger<PaymentService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<(string? PaymentUrl, string? Error)> StartPaymentAsync(int orderId, int customerId, bool isAdmin, CancellationToken ct)
    {
        // await using var tx = await _db.Database.BeginTransactionAsync();

        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && (isAdmin == true || o.CustomerId == customerId), ct);
            // .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return (null, "Order not found");
        if (order.PaymentStatus == PaymentStatus.Paid) return (null, "Order is already paid");
        if (order.PaymentStatus == PaymentStatus.Pending && order.PaymentUrl is not null)
            return (order.PaymentUrl, null);

        var totalPrice = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        var payment = new CreatePaymentRequest($"order-{orderId}", totalPrice, $"Payment for order {orderId}", order.Customer.Email);
        
        // Do they payment gateway call
        var result = await _gateway.CreateInvoiceAsync(payment, ct);
        // Check if the call is okay
        // Check if the payment
        // Set the order payment reference, url and status
        order.PaymentReference = result.GatewayId;
        order.PaymentUrl = result.PaymentUrl;
        order.PaymentStatus = PaymentStatus.Pending;

        await _db.SaveChangesAsync(ct);

        return (result.PaymentUrl, null);
    }

    public async Task ApplyCallbackAsync(XenditInvoiceCallback payload, CancellationToken ct)
    {
        var referenceId = payload.ExternalId;

        // string[] identifier = referenceId.Split("-", StringSplitOptions.None);
        const string prefix = "order-";

        if (!payload.ExternalId.StartsWith(prefix) ||
            !int.TryParse(payload.ExternalId[prefix.Length..], out var orderId))
        {
            _logger.LogWarning("Webhook with unrecognised external_id {ExternalId}", payload.ExternalId);
            return;
        }

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null)
        {
            _logger.LogWarning("Webhook for unknown order {OrderId}", orderId);
            return;
        }

        if (order.PaymentStatus == PaymentStatus.Paid) return;


        if (payload.Status == "PAID" || payload.Status == "SETTLED")
        {
            var totalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

            if (payload.Amount != totalAmount)
            {
                _logger.LogWarning("Payment amount and order amount does not match.");
                return;
            }

            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt = payload.PaidAt ?? DateTime.UtcNow;

        }

        // What if payload status is paid but the system payment status is expired, who to trust?

        else if (payload.Status == "EXPIRED")
        {
            order.PaymentStatus = PaymentStatus.Expired;
        }

        await _db.SaveChangesAsync(ct);
    }
}