using System.Threading;
using System.Threading.Tasks;
using TravelOrchestrator.Worker.Saga;

namespace TravelOrchestrator.Worker.Services;

public interface ITravelCompensationService
{
    Task CompensateAsync(TravelPackageState state, CancellationToken cancellationToken);
}
