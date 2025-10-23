using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public class HttpCarRentalService : ICarRentalService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpCarRentalService> _logger;

    public HttpCarRentalService(HttpClient httpClient, ILogger<HttpCarRentalService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ReserveCarAsync(TravelerInfo traveler, TripDetails trip, CarRentalPreferences preferences, CancellationToken cancellationToken)
    {
        var payload = new
        {
            traveler.CustomerId,
            traveler.Email,
            traveler.FullName,
            trip.Destination,
            trip.DepartureDate,
            trip.ReturnDate,
            preferences.CarClass,
            preferences.IncludeInsurance
        };

        _logger.LogInformation("Sending car reservation request for {CustomerId}", traveler.CustomerId);

        using var response = await _httpClient.PostAsJsonAsync("api/reservations", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new TravelServiceException("Car", $"Failed to reserve car ({response.StatusCode})", response.StatusCode, content);
        }

        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken);
        if (reservation?.ReservationId is null)
        {
            throw new TravelServiceException("Car", "Reservation response did not contain an identifier", response.StatusCode);
        }

        _logger.LogInformation("Car reserved with id {ReservationId}", reservation.ReservationId);
        return reservation.ReservationId;
    }

    public async Task CancelCarAsync(string reservationId, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Cancelling car reservation {ReservationId}", reservationId);
        using var response = await _httpClient.DeleteAsync($"api/reservations/{reservationId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Unable to cancel car reservation {ReservationId}: {Status} {Body}", reservationId, response.StatusCode, content);
        }
    }

    private record ReservationResponse(string ReservationId);
}
