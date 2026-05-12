using Umbraco.Community.LegacyFeatureConverter.Hubs;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Hubs;

[TestClass]
public class SignalRProgressReporterTests
{
    private Mock<IHubContext<ConversionHub, IConversionHubClient>> _hubContextMock = null!;
    private Mock<IHubClients<IConversionHubClient>> _clientsMock = null!;
    private Mock<IConversionHubClient> _clientProxyMock = null!;
    private Mock<ILogger<SignalRProgressReporter>> _loggerMock = null!;
    private Guid _queueItemId;

    [TestInitialize]
    public void Setup()
    {
        _hubContextMock = new Mock<IHubContext<ConversionHub, IConversionHubClient>>();
        _clientsMock = new Mock<IHubClients<IConversionHubClient>>();
        _clientProxyMock = new Mock<IConversionHubClient>();
        _loggerMock = new Mock<ILogger<SignalRProgressReporter>>();
        _queueItemId = Guid.NewGuid();

        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        _clientProxyMock.Setup(c => c.ReceiveProgress(It.IsAny<ConversionProgress>()))
            .Returns(Task.CompletedTask);
        _clientProxyMock.Setup(c => c.ConversionCompleted(
                It.IsAny<Guid>(), It.IsAny<ConversionStatus>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    private SignalRProgressReporter CreateReporter()
    {
        return new SignalRProgressReporter(
            _hubContextMock.Object, _queueItemId, _loggerMock.Object);
    }

    [TestMethod]
    public void Report_CallsReceiveProgressOnAllClients()
    {
        var reporter = CreateReporter();
        var progress = new ConversionProgress
        {
            ConversionId = Guid.NewGuid(),
            Phase = "Converting content",
            CurrentItem = "Test Page",
            ProcessedCount = 5,
            TotalCount = 10,
            Status = ConversionStatus.Running
        };

        ((IProgress<ConversionProgress>)reporter).Report(progress);

        // Allow async fire-and-forget to complete
        Thread.Sleep(50);

        _clientProxyMock.Verify(
            c => c.ReceiveProgress(It.Is<ConversionProgress>(p =>
                p.Phase == "Converting content" &&
                p.CurrentItem == "Test Page" &&
                p.ProcessedCount == 5 &&
                p.TotalCount == 10)),
            Times.Once);
    }

    [TestMethod]
    public void Report_SetsQueueItemIdOnProgress()
    {
        var reporter = CreateReporter();
        var progress = new ConversionProgress
        {
            ConversionId = Guid.NewGuid(),
            Phase = "Creating data types",
            ProcessedCount = 1,
            TotalCount = 5,
        };

        ((IProgress<ConversionProgress>)reporter).Report(progress);

        Thread.Sleep(50);

        _clientProxyMock.Verify(
            c => c.ReceiveProgress(It.Is<ConversionProgress>(p =>
                p.QueueItemId == _queueItemId)),
            Times.Once);
    }

    [TestMethod]
    public async Task SendCompletedAsync_CallsConversionCompletedOnAllClients()
    {
        var reporter = CreateReporter();
        var historyId = Guid.NewGuid();

        await reporter.SendCompletedAsync(
            ConversionStatus.Completed, historyId);

        _clientProxyMock.Verify(
            c => c.ConversionCompleted(
                _queueItemId,
                ConversionStatus.Completed,
                historyId),
            Times.Once);
    }

    [TestMethod]
    public async Task SendCompletedAsync_WithNullHistoryId_PassesNull()
    {
        var reporter = CreateReporter();

        await reporter.SendCompletedAsync(
            ConversionStatus.Failed, null);

        _clientProxyMock.Verify(
            c => c.ConversionCompleted(
                _queueItemId,
                ConversionStatus.Failed,
                null),
            Times.Once);
    }

    [TestMethod]
    public void Report_Throttles_RapidCalls()
    {
        var reporter = CreateReporter();

        // Fire many rapid updates
        for (int i = 0; i < 20; i++)
        {
            ((IProgress<ConversionProgress>)reporter).Report(new ConversionProgress
            {
                ConversionId = Guid.NewGuid(),
                Phase = "Converting content",
                CurrentItem = $"Item {i}",
                ProcessedCount = i,
                TotalCount = 20,
            });
        }

        // Allow async fire-and-forget to complete
        Thread.Sleep(100);

        // First call should always go through.
        // Subsequent calls within 250ms should be throttled.
        // Exact count depends on timing, but it should be less than 20.
        var callCount = _clientProxyMock.Invocations
            .Count(i => i.Method.Name == nameof(IConversionHubClient.ReceiveProgress));

        Assert.IsTrue(callCount >= 1, "At least the first report should be sent");
        Assert.IsTrue(callCount < 20, $"Expected throttled calls < 20, but got {callCount}");
    }

    [TestMethod]
    public void Report_AlwaysSendsFirstUpdate()
    {
        var reporter = CreateReporter();
        var progress = new ConversionProgress
        {
            ConversionId = Guid.NewGuid(),
            Phase = "Determining document types",
            ProcessedCount = 0,
            TotalCount = 0,
        };

        ((IProgress<ConversionProgress>)reporter).Report(progress);

        Thread.Sleep(50);

        _clientProxyMock.Verify(
            c => c.ReceiveProgress(It.Is<ConversionProgress>(p =>
                p.Phase == "Determining document types")),
            Times.Once);
    }

    [TestMethod]
    public async Task Report_SendsPendingUpdate_WhenFlushCalled()
    {
        var reporter = CreateReporter();

        // First report goes through immediately
        ((IProgress<ConversionProgress>)reporter).Report(new ConversionProgress
        {
            Phase = "Converting content",
            ProcessedCount = 0,
            TotalCount = 100,
        });

        // Second report gets throttled
        ((IProgress<ConversionProgress>)reporter).Report(new ConversionProgress
        {
            Phase = "Converting content",
            ProcessedCount = 50,
            TotalCount = 100,
        });

        // Flush forces the pending update out
        await reporter.FlushAsync();

        Thread.Sleep(50);

        // Should have sent both the first and the flushed pending update
        _clientProxyMock.Verify(
            c => c.ReceiveProgress(It.IsAny<ConversionProgress>()),
            Times.Exactly(2));
    }
}
