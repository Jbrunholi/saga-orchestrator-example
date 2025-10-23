using System;
using System.Net.Http;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TravelOrchestrator.Worker.Saga;
using TravelOrchestrator.Worker.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient<IFlightReservationService, HttpFlightReservationService>(client =>
        {
            ConfigureClient(client, context.Configuration, "Services:Flight");
        });

        services.AddHttpClient<IHotelReservationService, HttpHotelReservationService>(client =>
        {
            ConfigureClient(client, context.Configuration, "Services:Hotel");
        });

        services.AddHttpClient<ICarRentalService, HttpCarRentalService>(client =>
        {
            ConfigureClient(client, context.Configuration, "Services:Car");
        });

        services.AddHttpClient<IPaymentService, HttpPaymentService>(client =>
        {
            ConfigureClient(client, context.Configuration, "Services:Payment");
        });

        services.AddSingleton<ITravelCompensationService, TravelCompensationService>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddSagaStateMachine<TravelPackageStateMachine, TravelPackageState>()
                .InMemoryRepository();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbit = context.Configuration.GetSection("RabbitMq");
                var hostName = rabbit.GetValue<string>("Host") ?? "localhost";
                var virtualHost = rabbit.GetValue<string>("VirtualHost") ?? "/";
                cfg.Host(hostName, virtualHost, h =>
                {
                    h.Username(rabbit.GetValue<string>("Username") ?? "guest");
                    h.Password(rabbit.GetValue<string>("Password") ?? "guest");
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

static void ConfigureClient(HttpClient client, IConfiguration configuration, string key)
{
    var baseAddress = configuration.GetValue<string>(key);
    if (string.IsNullOrWhiteSpace(baseAddress))
    {
        throw new InvalidOperationException($"Configuration value '{key}' was not found.");
    }

    client.BaseAddress = new Uri(baseAddress);
    client.Timeout = TimeSpan.FromSeconds(30);
}
