using Amazon;
using Amazon.SQS;
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

        services.AddOptions<SqsOptions>()
            .Bind(configuration.GetSection(SqsOptions.SectionName))
            .Validate(options => options.HasValidQueueUrl(),
                "Sqs:QueueUrl must be an HTTPS SQS queue URL in Sqs:Region.")
            .ValidateOnStart();
        services.AddSingleton<IAmazonSQS>(provider => new AmazonSQSClient(new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(provider.GetRequiredService<IOptions<SqsOptions>>().Value.Region),
            Timeout = TimeSpan.FromSeconds(10),
            MaxErrorRetry = 2
        }));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOrderPaymentProcessor, OrderPaymentProcessor>();
        services.AddScoped<IPaymentProcessedPublisher, DualPaymentProcessedPublisher>();

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
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Ignore<InvalidOrderMessageException>();
                        retry.Intervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
                    });
                    endpoint.ConfigureConsumer<OrderPlacedConsumer>(context);
                });
            });
        });

        return services;
    }
}
