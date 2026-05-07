using LP.Umbraco.LegacyFeatureConverter.Hubs;
using LP.Umbraco.LegacyFeatureConverter.Models;
using LP.Umbraco.LegacyFeatureConverter.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace LP.Umbraco.LegacyFeatureConverter.Tests.Hubs;

[TestClass]
public class SignalRProgressReporterFactoryTests
{
    private Mock<IHubContext<ConversionHub, IConversionHubClient>> _hubContextMock = null!;
    private Mock<IHubClients<IConversionHubClient>> _clientsMock = null!;
    private Mock<IConversionHubClient> _clientProxyMock = null!;
    private Mock<ILoggerFactory> _loggerFactoryMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _hubContextMock = new Mock<IHubContext<ConversionHub, IConversionHubClient>>();
        _clientsMock = new Mock<IHubClients<IConversionHubClient>>();
        _clientProxyMock = new Mock<IConversionHubClient>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();

        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);

        _loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger>());
    }

    private SignalRProgressReporterFactory CreateFactory()
    {
        return new SignalRProgressReporterFactory(
            _hubContextMock.Object, _loggerFactoryMock.Object);
    }

    [TestMethod]
    public void Create_ReturnsNonNullProgressReporter()
    {
        var factory = CreateFactory();

        var progress = factory.Create(Guid.NewGuid());

        Assert.IsNotNull(progress);
    }

    [TestMethod]
    public void Create_ReturnsDifferentInstancesPerCall()
    {
        var factory = CreateFactory();

        var progress1 = factory.Create(Guid.NewGuid());
        var progress2 = factory.Create(Guid.NewGuid());

        Assert.AreNotSame(progress1, progress2);
    }

    [TestMethod]
    public async Task SendCompletedAsync_DelegatesToHubClients()
    {
        var factory = CreateFactory();
        var queueItemId = Guid.NewGuid();
        var historyId = Guid.NewGuid();

        _clientProxyMock.Setup(c => c.ConversionCompleted(
                It.IsAny<Guid>(), It.IsAny<ConversionStatus>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        await factory.SendCompletedAsync(
            queueItemId, ConversionStatus.Completed, historyId);

        _clientProxyMock.Verify(
            c => c.ConversionCompleted(
                queueItemId,
                ConversionStatus.Completed,
                historyId),
            Times.Once);
    }
}
