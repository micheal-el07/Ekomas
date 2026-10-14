using System.Text.Json;

public record CreatePaymentRequest(string ExternalId, decimal Amount, string Description, string? PayerEmail);
public record CreatePaymentResult(string GatewayId, string PaymentUrl, string Status);

public class PaymentGatewayException : Exception
{
    public PaymentGatewayException(string message) : base(message) { }
    
}

public interface IPaymentGateway
{
    Task<CreatePaymentResult> CreateInvoiceAsync(CreatePaymentRequest request, CancellationToken ct);
}

public class XenditGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<XenditGateway> _logger;

    public XenditGateway(HttpClient http, IConfiguration config, ILogger<XenditGateway> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<CreatePaymentResult> CreateInvoiceAsync(CreatePaymentRequest r, CancellationToken ct)
    {
        var body = new
        {
            external_id = r.ExternalId,
            amount = r.Amount,
            description = r.Description,
            payer_email = r.PayerEmail,
            currency = _config["Xendit:Currency"]
        };

        using var response = await _http.PostAsJsonAsync("v2/invoices", body, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("Xendit create-invoice {Status}: {Body}", (int)response.StatusCode, json);

        if (!response.IsSuccessStatusCode)
            throw new PaymentGatewayException($"Xendit returned {(int)response.StatusCode}.");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new CreatePaymentResult(
            root.GetProperty("id").GetString()!,
            root.GetProperty("invoice_url").GetString()!,
            root.GetProperty("status").GetString()!
        );
    }
}