using FCG.Payments.Api.Configuration;
using FCG.Payments.Api.Consumers;
using FCG.Payments.Api.Messaging;
using FCG.Payments.Api.Payments;
using MassTransit;
using Microsoft.Extensions.Options;

namespace FCG.Payments.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddHealthChecks();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host),
                "RabbitMq:Host is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.VirtualHost),
                "RabbitMq:VirtualHost is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Username),
                "RabbitMq:Username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password),
                "RabbitMq:Password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.OrderPlacedQueue),
                "RabbitMq:OrderPlacedQueue is required.")
            .ValidateOnStart();

        services
            .AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOrderPaymentProcessor, OrderPaymentProcessor>();
        services.AddScoped<IPaymentProcessedPublisher, MassTransitPaymentProcessedPublisher>();

        services.AddMassTransit(configurator =>
        {
            configurator.AddConsumer<OrderPlacedConsumer>();

            configurator.UsingRabbitMq((context, rabbit) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                rabbit.Host(
                    options.Host,
                    options.Port,
                    options.VirtualHost,
                    host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });

                rabbit.ReceiveEndpoint(options.OrderPlacedQueue, endpoint =>
                {
                    endpoint.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });

        return services;
    }
}
