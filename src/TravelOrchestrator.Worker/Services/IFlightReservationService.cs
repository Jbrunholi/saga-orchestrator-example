using System.Threading;
using System.Threading.Tasks;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public interface IFlightReservationService
{
    Task<string> ReserveFlightAsync(TravelerInfo traveler, TripDetails trip, CancellationToken cancellationToken);

    Task CancelFlightAsync(string reservationId, CancellationToken cancellationToken);
}
