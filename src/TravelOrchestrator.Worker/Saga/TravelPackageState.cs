using System;
using MassTransit;
using TravelOrchestrator.Worker.Contracts;

namespace TravelOrchestrator.Worker.Saga;

public class TravelPackageState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }

    public string CurrentState { get; set; } = string.Empty;

    public string? FlightReservationId { get; set; }

    public string? HotelReservationId { get; set; }

    public string? CarReservationId { get; set; }

    public string? PaymentConfirmationId { get; set; }

    public string? TravelerEmail { get; set; }

    public string? TravelerName { get; set; }

    public string? CustomerId { get; set; }

    public decimal TotalAmount { get; set; }

    public TripDetails? Trip { get; set; }

    public AccommodationPreferences? Accommodation { get; set; }

    public CarRentalPreferences? CarRental { get; set; }

    public PaymentDetails? Payment { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
