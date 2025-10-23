using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public class HttpHotelReservationService : IHotelReservationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpHotelReservationService> _logger;

    public HttpHotelReservationService(HttpClient httpClient, ILogger<HttpHotelReservationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> ReserveHotelAsync(TravelerInfo traveler, TripDetails trip, AccommodationPreferences preferences, CancellationToken cancellationToken)
    {
        var payload = new
        {
            traveler.CustomerId,
            traveler.Email,
            traveler.FullName,
            trip.Destination,
            trip.DepartureDate,
            trip.ReturnDate,
            preferences.HotelCategory,
            preferences.IncludeBreakfast,
            trip.Travelers
        };

        _logger.LogInformation("Sending hotel reservation request for {CustomerId}", traveler.CustomerId);

        using var response = await _httpClient.PostAsJsonAsync("api/reservations", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new TravelServiceException("Hotel", $"Failed to reserve hotel ({response.StatusCode})", response.StatusCode, content);
        }

        var reservation = await response.Content.ReadFromJsonAsync<ReservationResponse>(cancellationToken: cancellationToken);
        if (reservation?.ReservationId is null)
        {
            throw new TravelServiceException("Hotel", "Reservation response did not contain an identifier", response.StatusCode);
        }

        _logger.LogInformation("Hotel reserved with id {ReservationId}", reservation.ReservationId);
        return reservation.ReservationId;
    }

    public async Task CancelHotelAsync(string reservationId, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Cancelling hotel reservation {ReservationId}", reservationId);
        using var response = await _httpClient.DeleteAsync($"api/reservations/{reservationId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Unable to cancel hotel reservation {ReservationId}: {Status} {Body}", reservationId, response.StatusCode, content);
        }
    }

    private record ReservationResponse(string ReservationId);
}
