using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TravelOrchestrator.Worker.Contracts;
using TravelOrchestrator.Worker.Services;

namespace TravelOrchestrator.Worker.Saga;

public class TravelPackageStateMachine : MassTransitStateMachine<TravelPackageState>
{
    public State AwaitingFlight { get; private set; } = null!;
    public State FlightReserved { get; private set; } = null!;
    public State HotelReserved { get; private set; } = null!;
    public State CarReserved { get; private set; } = null!;
    public State PaymentProcessed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<PurchaseTravelPackage> PurchaseRequested { get; private set; } = null!;
    public Event<ReservationCompleted> FlightReservationCompleted { get; private set; } = null!;
    public Event<ReservationCompleted> HotelReservationCompleted { get; private set; } = null!;
    public Event<ReservationCompleted> CarReservationCompleted { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<ReservationFailed> ReservationFailed { get; private set; } = null!;

    public TravelPackageStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PurchaseRequested, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FlightReservationCompleted, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));
        Event(() => HotelReservationCompleted, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CarReservationCompleted, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentCompleted, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));
        Event(() => ReservationFailed, cfg => cfg.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(PurchaseRequested)
                .Then(context =>
                {
                    context.Instance.CreatedAt = DateTime.UtcNow;
                    context.Instance.CustomerId = context.Data.Traveler.CustomerId;
                    context.Instance.TravelerEmail = context.Data.Traveler.Email;
                    context.Instance.TravelerName = context.Data.Traveler.FullName;
                    context.Instance.Trip = context.Data.Trip;
                    context.Instance.Accommodation = context.Data.Accommodation;
                    context.Instance.CarRental = context.Data.CarRental;
                    context.Instance.Payment = context.Data.Payment;
                    context.Instance.TotalAmount = CalculateTotalAmount(context.Data);
                })
                .TransitionTo(AwaitingFlight)
                .ThenAsync(ReserveFlight));

        During(AwaitingFlight,
            When(FlightReservationCompleted)
                .Then(context => context.Instance.FlightReservationId = context.Data.ReservationId)
                .TransitionTo(FlightReserved)
                .ThenAsync(RequestHotelReservation));

        During(FlightReserved,
            When(HotelReservationCompleted)
                .Then(context => context.Instance.HotelReservationId = context.Data.ReservationId)
                .TransitionTo(HotelReserved)
                .ThenAsync(RequestCarReservation));

        During(HotelReserved,
            When(CarReservationCompleted)
                .Then(context => context.Instance.CarReservationId = context.Data.ReservationId)
                .TransitionTo(CarReserved)
                .ThenAsync(ProcessPayment));

        During(CarReserved,
            When(PaymentCompleted)
                .Then(context =>
                {
                    context.Instance.PaymentConfirmationId = context.Data.ConfirmationId;
                    context.Instance.CompletedAt = DateTime.UtcNow;
                })
                .TransitionTo(PaymentProcessed)
                .ThenAsync(PublishCompletion)
                .Finalize());

        DuringAny(
            When(ReservationFailed)
                .Then(context => context.Instance.FailureReason = context.Data.Reason)
                .ThenAsync(RunCompensation)
                .ThenAsync(PublishFailure)
                .TransitionTo(Failed)
                .Finalize());

        SetCompletedWhenFinalized();
    }

    private async Task ReserveFlight(BehaviorContext<TravelPackageState, PurchaseTravelPackage> context)
    {
        var provider = context.GetPayload<IServiceProvider>();
        var flightService = provider.GetRequiredService<IFlightReservationService>();
        var logger = provider.GetRequiredService<ILogger<TravelPackageStateMachine>>();

        try
        {
            var reservationId = await flightService.ReserveFlightAsync(context.Data.Traveler, context.Data.Trip, context.CancellationToken);
            await RaiseEvent(context.Instance, FlightReservationCompleted, new ReservationCompleted(context.Instance.CorrelationId, reservationId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reserve flight for {CorrelationId}", context.Instance.CorrelationId);
            await RaiseEvent(context.Instance, ReservationFailed, new ReservationFailed(context.Instance.CorrelationId, $"Flight reservation failed: {ex.Message}"));
        }
    }

    private async Task RequestHotelReservation(BehaviorContext<TravelPackageState, ReservationCompleted> context)
    {
        var provider = context.GetPayload<IServiceProvider>();
        var hotelService = provider.GetRequiredService<IHotelReservationService>();
        var logger = provider.GetRequiredService<ILogger<TravelPackageStateMachine>>();

        try
        {
            if (context.Instance.Trip is null || context.Instance.Accommodation is null)
            {
                throw new InvalidOperationException("Trip or accommodation details are missing in saga state");
            }

            var traveler = BuildTravelerInfo(context.Instance);
            var reservationId = await hotelService.ReserveHotelAsync(traveler, context.Instance.Trip, context.Instance.Accommodation, context.CancellationToken);
            await RaiseEvent(context.Instance, HotelReservationCompleted, new ReservationCompleted(context.Instance.CorrelationId, reservationId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reserve hotel for {CorrelationId}", context.Instance.CorrelationId);
            await RaiseEvent(context.Instance, ReservationFailed, new ReservationFailed(context.Instance.CorrelationId, $"Hotel reservation failed: {ex.Message}"));
        }
    }

    private async Task RequestCarReservation(BehaviorContext<TravelPackageState, ReservationCompleted> context)
    {
        var provider = context.GetPayload<IServiceProvider>();
        var carService = provider.GetRequiredService<ICarRentalService>();
        var logger = provider.GetRequiredService<ILogger<TravelPackageStateMachine>>();

        try
        {
            if (context.Instance.Trip is null || context.Instance.CarRental is null)
            {
                throw new InvalidOperationException("Trip or car rental details are missing in saga state");
            }

            var traveler = BuildTravelerInfo(context.Instance);
            var reservationId = await carService.ReserveCarAsync(traveler, context.Instance.Trip, context.Instance.CarRental, context.CancellationToken);
            await RaiseEvent(context.Instance, CarReservationCompleted, new ReservationCompleted(context.Instance.CorrelationId, reservationId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reserve car for {CorrelationId}", context.Instance.CorrelationId);
            await RaiseEvent(context.Instance, ReservationFailed, new ReservationFailed(context.Instance.CorrelationId, $"Car reservation failed: {ex.Message}"));
        }
    }

    private async Task ProcessPayment(BehaviorContext<TravelPackageState, ReservationCompleted> context)
    {
        var provider = context.GetPayload<IServiceProvider>();
        var paymentService = provider.GetRequiredService<IPaymentService>();
        var logger = provider.GetRequiredService<ILogger<TravelPackageStateMachine>>();

        try
        {
            if (context.Instance.Payment is null)
            {
                throw new InvalidOperationException("Payment details are missing in saga state");
            }

            var traveler = BuildTravelerInfo(context.Instance);
            var confirmationId = await paymentService.ProcessPaymentAsync(traveler, context.Instance.Payment, context.Instance.TotalAmount, context.CancellationToken);
            await RaiseEvent(context.Instance, PaymentCompleted, new PaymentCompleted(context.Instance.CorrelationId, confirmationId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process payment for {CorrelationId}", context.Instance.CorrelationId);
            await RaiseEvent(context.Instance, ReservationFailed, new ReservationFailed(context.Instance.CorrelationId, $"Payment failed: {ex.Message}"));
        }
    }

    private async Task PublishCompletion(BehaviorContext<TravelPackageState, PaymentCompleted> context)
    {
        await context.Publish(new TravelPackageCompleted(context.Instance.CorrelationId, context.Data.ConfirmationId, DateTime.UtcNow));
    }

    private async Task RunCompensation(BehaviorContext<TravelPackageState, ReservationFailed> context)
    {
        var provider = context.GetPayload<IServiceProvider>();
        var compensation = provider.GetRequiredService<ITravelCompensationService>();
        var logger = provider.GetRequiredService<ILogger<TravelPackageStateMachine>>();

        logger.LogWarning("Compensating saga {CorrelationId} because {Reason}", context.Instance.CorrelationId, context.Data.Reason);
        await compensation.CompensateAsync(context.Instance, context.CancellationToken);
    }

    private Task PublishFailure(BehaviorContext<TravelPackageState, ReservationFailed> context)
    {
        return context.Publish(new TravelPackageFailed(context.Instance.CorrelationId, context.Data.Reason, DateTime.UtcNow));
    }

    private static TravelerInfo BuildTravelerInfo(TravelPackageState state)
    {
        if (state.CustomerId is null || state.TravelerEmail is null || state.TravelerName is null)
        {
            throw new InvalidOperationException("Traveler information not found in saga state");
        }

        return new TravelerInfo(state.CustomerId, state.TravelerEmail, state.TravelerName);
    }

    private static decimal CalculateTotalAmount(PurchaseTravelPackage command)
    {
        const decimal baseFlight = 650m;
        const decimal hotelPerNight = 180m;
        const decimal carPerDay = 75m;
        const decimal paymentFee = 25m;

        var travelDays = Math.Max((decimal)(command.Trip.ReturnDate - command.Trip.DepartureDate).TotalDays, 1);

        var flightTotal = baseFlight * command.Trip.Travelers;
        var hotelTotal = hotelPerNight * command.Trip.Travelers * travelDays;
        var carTotal = command.CarRental.IncludeInsurance ? (carPerDay + 30m) * travelDays : carPerDay * travelDays;

        return Math.Round(flightTotal + hotelTotal + carTotal + paymentFee, 2);
    }
}

public record ReservationCompleted(Guid CorrelationId, string ReservationId);

public record PaymentCompleted(Guid CorrelationId, string ConfirmationId);

public record ReservationFailed(Guid CorrelationId, string Reason);
