public enum PaymentStatus { Unpaid, Pending, Paid, Expired }
public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public List<OrderItem> Items { get; set; } = new();
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? PaymentReference { get; set; }
    public string? PaymentUrl { get; set; }
    public DateTime? PaidAt { get; set; }
}