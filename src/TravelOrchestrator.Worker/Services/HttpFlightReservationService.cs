using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public class HttpFlightReservationService : IFlightReservationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpFlightReservationService> _logger;

    public HttpFlightReservationService(HttpClient httpClient, ILogger<HttpFlightReservationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ReserveFlightAsync(TravelerInfo traveler, TripDetails trip, CancellationToken cancellationToken)
    {
        var payload = new
        {
            traveler.CustomerId,
            traveler.Email,
            traveler.FullName,
            trip.Origin,
            trip.Destination,
            trip.DepartureDate,
            trip.ReturnDate,
            trip.Travelers
        };

        _logger.LogInformation("Sending flight reservation request for {CustomerId}", traveler.CustomerId);

        using var response = await _httpClient.PostAsJsonAsync("api/reservations", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new TravelServiceException("Flight", $"Failed to reserve flight ({response.StatusCode})", response.StatusCode, content);
        }

        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken);
        if (reservation?.ReservationId is null)
        {
            throw new TravelServiceException("Flight", "Reservation response did not contain an identifier", response.StatusCode);
        }

        _logger.LogInformation("Flight reserved with id {ReservationId}", reservation.ReservationId);
        return reservation.ReservationId;
    }

    public async Task CancelFlightAsync(string reservationId, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Cancelling flight reservation {ReservationId}", reservationId);
        using var response = await _httpClient.DeleteAsync($"api/reservations/{reservationId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Unable to cancel flight reservation {ReservationId}: {Status} {Body}", reservationId, response.StatusCode, content);
        }
    }

    private record ReservationResponse(string ReservationId);
}
