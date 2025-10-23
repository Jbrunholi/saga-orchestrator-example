using System;

namespace TravelOrchestrator.Worker.Contracts;

public record TravelerInfo(string CustomerId, string Email, string FullName);

public record TripDetails(string Origin, string Destination, DateTime DepartureDate, DateTime ReturnDate, int Travelers);

public record AccommodationPreferences(string HotelCategory, bool IncludeBreakfast);

public record CarRentalPreferences(string CarClass, bool IncludeInsurance);

public record PaymentDetails(string CardNumber, string CardHolder, string Expiration, string Cvv);

public record PurchaseTravelPackage(Guid CorrelationId, TravelerInfo Traveler, TripDetails Trip, AccommodationPreferences Accommodation, CarRentalPreferences CarRental, PaymentDetails Payment);

public record TravelPackageCompleted(Guid CorrelationId, string ConfirmationCode, DateTime Timestamp);

public record TravelPackageFailed(Guid CorrelationId, string Reason, DateTime Timestamp);
