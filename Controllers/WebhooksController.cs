using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

public class XenditInvoiceCallback
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("external_id")] public string ExternalId { get; set; } = "";
    [JsonPropertyName("status")] public string Status{ get; set; } = "";
    [JsonPropertyName("amount")] public decimal Amount { get; set; }
    [JsonPropertyName("paid_at")] public DateTime? PaidAt { get; set; }
}

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IPaymentService _payments;
    private readonly ILogger<WebhooksController> _logger;
    public WebhooksController(ILogger<WebhooksController> logger, IConfiguration config, IPaymentService payment)
    {
        _config = config;
        _payments = payment;
        _logger = logger;
    }

    [HttpPost("xendit")]
    public async Task<IActionResult> Xendit(
        [FromBody] XenditInvoiceCallback payload,
        [FromHeader(Name = "x-callback-token")] string? token,
        CancellationToken ct
    )
    {
        var expected = _config["Xendit:CallbackToken"] ?? "";

        var valid = token is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected)
        );

        if (!valid) return Unauthorized();

        await _payments.ApplyCallbackAsync(payload, ct);
        return Ok();
    }
}