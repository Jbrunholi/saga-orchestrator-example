using System.Threading;
using System.Threading.Tasks;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Services;

public interface IPaymentService
{
    Task<string> ProcessPaymentAsync(TravelerInfo traveler, PaymentDetails payment, decimal totalAmount, CancellationToken cancellationToken);

    Task RefundPaymentAsync(string confirmationId, CancellationToken cancellationToken);
}
