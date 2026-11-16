using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using FCG.IntegrationEvents.V1;
using FCG.Payments.Api.Configuration;
using FCG.Payments.Api.Consumers;
using FCG.Payments.Api.Messaging;
using FCG.Payments.Tests.Payments;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FCG.Payments.Tests.Consumers;

public sealed class DualPublicationTests
{
    private const string QueueUrl = "https://sqs.us-east-1.amazonaws.com/123456789012/fcg-fase3-payment-processed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(59.90, PaymentStatuses.Approved)]
    [InlineData(150.00, PaymentStatuses.Rejected)]
    public async Task Consumer_PublishesSameContractToBothDestinations(decimal price, string status)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var rabbit = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var sqs = new Mock<IAmazonSQS>(MockBehavior.Strict);
        PaymentProcessedEvent? rabbitEvent = null;
        SendMessageRequest? sent = null;
        rabbit.Setup(p => p.Publish(It.IsAny<PaymentProcessedEvent>(), cancellation))
            .Callback<PaymentProcessedEvent, CancellationToken>((message, _) => rabbitEvent = message)
            .Returns(Task.CompletedTask);
        sqs.Setup(p => p.SendMessageAsync(It.IsAny<SendMessageRequest>(), cancellation))
            .Callback<SendMessageRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new SendMessageResponse());

        var order = OrderPaymentProcessorTests.CreateOrder(price);
        await Consumer(rabbit.Object, sqs.Object).ProcessAsync(order, cancellation);

        Assert.NotNull(sent);
        Assert.Equal(QueueUrl, sent.QueueUrl);
        var notification = JsonSerializer.Deserialize<PaymentProcessedEvent>(sent.MessageBody, JsonOptions);
        Assert.Equal(rabbitEvent, notification);
        Assert.Equal(order.OrderId, notification!.OrderId);
        Assert.Equal(status, notification.Status);
        Assert.DoesNotContain("messageType", sent.MessageBody);
        rabbit.VerifyAll();
        sqs.VerifyAll();
    }

    [Fact]
    public async Task SqsFailure_PropagatesAndRetryRepublishesSameOrderToBothDestinations()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var rabbit = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var sqs = new Mock<IAmazonSQS>(MockBehavior.Strict);
        var rabbitEvents = new List<PaymentProcessedEvent>();
        var sqsBodies = new List<string>();
        var fail = true;
        rabbit.Setup(p => p.Publish(It.IsAny<PaymentProcessedEvent>(), cancellation))
            .Callback<PaymentProcessedEvent, CancellationToken>((message, _) => rabbitEvents.Add(message))
            .Returns(Task.CompletedTask);
        sqs.Setup(p => p.SendMessageAsync(It.IsAny<SendMessageRequest>(), cancellation))
            .Returns<SendMessageRequest, CancellationToken>((request, _) =>
            {
                sqsBodies.Add(request.MessageBody);
                return fail ? Task.FromException<SendMessageResponse>(new AmazonSQSException("Unavailable"))
                    : Task.FromResult(new SendMessageResponse());
            });
        var consumer = Consumer(rabbit.Object, sqs.Object);
        var order = OrderPaymentProcessorTests.CreateOrder(59.90m);

        await Assert.ThrowsAsync<AmazonSQSException>(() => consumer.ProcessAsync(order, cancellation));
        Assert.Single(rabbitEvents);
        fail = false;
        await consumer.ProcessAsync(order, cancellation);

        Assert.Equal(2, rabbitEvents.Count);
        Assert.All(rabbitEvents, message => Assert.Equal(order.OrderId, message.OrderId));
        Assert.All(sqsBodies, body => Assert.Equal(order.OrderId,
            JsonSerializer.Deserialize<PaymentProcessedEvent>(body, JsonOptions)!.OrderId));
    }

    [Fact]
    public async Task RabbitFailure_PropagatesWithoutSendingSqs_AndRetryCompletes()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var rabbit = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var sqs = new Mock<IAmazonSQS>(MockBehavior.Strict);
        rabbit.SetupSequence(p => p.Publish(It.IsAny<PaymentProcessedEvent>(), cancellation))
            .ThrowsAsync(new IOException("Broker unavailable"))
            .Returns(Task.CompletedTask);
        sqs.Setup(p => p.SendMessageAsync(It.IsAny<SendMessageRequest>(), cancellation))
            .ReturnsAsync(new SendMessageResponse());
        var consumer = Consumer(rabbit.Object, sqs.Object);
        var order = OrderPaymentProcessorTests.CreateOrder(59.90m);

        await Assert.ThrowsAsync<IOException>(() => consumer.ProcessAsync(order, cancellation));
        sqs.Verify(p => p.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        await consumer.ProcessAsync(order, cancellation);
        sqs.Verify(p => p.SendMessageAsync(It.IsAny<SendMessageRequest>(), cancellation), Times.Once);
    }

    private static OrderPlacedConsumer Consumer(IPublishEndpoint rabbit, IAmazonSQS sqs) => new(
        OrderPaymentProcessorTests.CreateProcessor(),
        new DualPaymentProcessedPublisher(rabbit, sqs, Options.Create(new SqsOptions { QueueUrl = QueueUrl })),
        NullLogger<OrderPlacedConsumer>.Instance);
}
