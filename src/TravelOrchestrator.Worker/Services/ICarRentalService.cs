using System.Threading;
using System.Threading.Tasks;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public interface ICarRentalService
{
    Task<string> ReserveCarAsync(TravelerInfo traveler, TripDetails trip, CarRentalPreferences preferences, CancellationToken cancellationToken);

    Task CancelCarAsync(string reservationId, CancellationToken cancellationToken);
}
