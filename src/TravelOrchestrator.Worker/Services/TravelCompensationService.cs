using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Saga;

namespace TravelOrchestrator.Worker.Services;

public class TravelCompensationService : ITravelCompensationService
{
    private readonly IFlightReservationService _flightReservationService;
    private readonly IHotelReservationService _hotelReservationService;
    private readonly ICarRentalService _carRentalService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<TravelCompensationService> _logger;

    public TravelCompensationService(
        IFlightReservationService flightReservationService,
        IHotelReservationService hotelReservationService,
        ICarRentalService carRentalService,
        IPaymentService paymentService,
        ILogger<TravelCompensationService> logger)
    {
        _flightReservationService = flightReservationService;
        _hotelReservationService = hotelReservationService;
        _carRentalService = carRentalService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task CompensateAsync(TravelPackageState state, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Running compensation for travel package {CorrelationId}", state.CorrelationId);

        if (state.PaymentConfirmationId is not null)
        {
            await _paymentService.RefundPaymentAsync(state.PaymentConfirmationId, cancellationToken);
        }

        if (state.CarReservationId is not null)
        {
            await _carRentalService.CancelCarAsync(state.CarReservationId, cancellationToken);
        }

        if (state.HotelReservationId is not null)
        {
            await _hotelReservationService.CancelHotelAsync(state.HotelReservationId, cancellationToken);
        }

        if (state.FlightReservationId is not null)
        {
            await _flightReservationService.CancelFlightAsync(state.FlightReservationId, cancellationToken);
        }
    }
}
