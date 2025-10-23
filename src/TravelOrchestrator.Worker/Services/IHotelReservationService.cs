using System.Threading;
using System.Threading.Tasks;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public interface IHotelReservationService
{
    Task<string> ReserveHotelAsync(TravelerInfo traveler, TripDetails trip, AccommodationPreferences preferences, CancellationToken cancellationToken);

    Task CancelHotelAsync(string reservationId, CancellationToken cancellationToken);
}
