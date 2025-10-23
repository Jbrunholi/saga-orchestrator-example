using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public class HttpPaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpPaymentService> _logger;

    public HttpPaymentService(HttpClient httpClient, ILogger<HttpPaymentService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ProcessPaymentAsync(TravelerInfo traveler, PaymentDetails payment, decimal totalAmount, CancellationToken cancellationToken)
    {
        var payload = new
        {
            traveler.CustomerId,
            traveler.Email,
            payment.CardNumber,
            payment.CardHolder,
            payment.Expiration,
            payment.Cvv,
            Amount = totalAmount
        };

        _logger.LogInformation("Processing payment of {Amount} for {CustomerId}", totalAmount, traveler.CustomerId);

        using var response = await _httpClient.PostAsJsonAsync("api/payments", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new TravelServiceException("Payment", $"Failed to process payment ({response.StatusCode})", response.StatusCode, content);
        }

        var confirmation = await response.Content.ReadFromJsonAsync<PaymentResponse>(cancellationToken: cancellationToken);
        if (confirmation?.ConfirmationId is null)
        {
            throw new TravelServiceException("Payment", "Payment response did not contain an identifier", response.StatusCode);
        }

        _logger.LogInformation("Payment confirmed with id {ConfirmationId}", confirmation.ConfirmationId);
        return confirmation.ConfirmationId;
    }

    public async Task RefundPaymentAsync(string confirmationId, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Refunding payment {ConfirmationId}", confirmationId);
        using var response = await _httpClient.PostAsJsonAsync($"api/payments/{confirmationId}/refund", new { }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Unable to refund payment {ConfirmationId}: {Status} {Body}", confirmationId, response.StatusCode, content);
        }
    }

    private record PaymentResponse(string ConfirmationId);
}
