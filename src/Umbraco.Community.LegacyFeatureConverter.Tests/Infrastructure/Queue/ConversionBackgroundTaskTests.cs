using System.Text.Json;
using Umbraco.Community.LegacyFeatureConverter.Converters;
using Umbraco.Community.LegacyFeatureConverter.Infrastructure.Queue;
using Umbraco.Community.LegacyFeatureConverter.Models;
using Umbraco.Community.LegacyFeatureConverter.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

// ReSharper disable AccessToDisposedClosure

namespace Umbraco.Community.LegacyFeatureConverter.Tests.Infrastructure.Queue;

[TestClass]
public class ConversionBackgroundTaskTests
{
    private Mock<IServiceScopeFactory> _scopeFactoryMock = null!;
    private Mock<IServiceScope> _scopeMock = null!;
    private Mock<IServiceProvider> _serviceProviderMock = null!;
    private Mock<IConversionQueueService> _queueServiceMock = null!;
    private Mock<IConverterService> _converterServiceMock = null!;
    private Mock<IProgressReporterFactory> _progressFactoryMock = null!;
    private Mock<ILogger<ConversionBackgroundTask>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _queueServiceMock = new Mock<IConversionQueueService>();
        _converterServiceMock = new Mock<IConverterService>();
        _progressFactoryMock = new Mock<IProgressReporterFactory>();
        _loggerMock = new Mock<ILogger<ConversionBackgroundTask>>();

        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock.Setup(p => p.GetService(typeof(IConversionQueueService)))
            .Returns(_queueServiceMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(IConverterService)))
            .Returns(_converterServiceMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(IProgressReporterFactory)))
            .Returns(_progressFactoryMock.Object);

        _progressFactoryMock.Setup(f => f.Create(It.IsAny<Guid>()))
            .Returns(Mock.Of<IProgress<ConversionProgress>>());
        _progressFactoryMock.Setup(f => f.SendCompletedAsync(
                It.IsAny<Guid>(), It.IsAny<ConversionStatus>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Creates the background task and runs it briefly, then cancels.
    /// </summary>
    private async Task RunTaskOnceAsync(ConversionBackgroundTask task)
    {
        using var cts = new CancellationTokenSource();

        // Set up the queue to return null on dequeue (empty queue)
        // so the task exits the processing loop quickly
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem?)null)
            .Callback(() => cts.Cancel()); // Cancel after first poll

        await task.StartAsync(cts.Token);
        // Give it a moment to process
        try { await Task.Delay(200, cts.Token); } catch (OperationCanceledException) { }
        await task.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task OnStartup_ResetsOrphanedItems()
    {
        _queueServiceMock.Setup(q => q.ResetOrphanedItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        await RunTaskOnceAsync(task);

        _queueServiceMock.Verify(
            q => q.ResetOrphanedItemsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenQueueEmpty_DoesNothing()
    {
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((QueueItem?)null);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await task.StartAsync(cts.Token);
        try { await Task.Delay(400); } catch { }
        await task.StopAsync(CancellationToken.None);

        // Should have attempted dequeue but not called any converter
        _converterServiceMock.Verify(
            c => c.GetLegacyConverterByName(It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task WhenItemDequeued_ExecutesConverter()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? queueItem : null;
            });

        var converterMock = new Mock<IPropertyConverter>();
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        // Verify converter was called
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => o.ConverterType == "Test Converter"),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify queue item was completed
        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Completed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenMacroConverterDequeued_DispatchesToMacroConverter()
    {
        // Mirror the property-converter happy-path test for the macro family.
        var options = new ConversionOptions
        {
            ConverterType = "Macro to Rich Text Block",
            SelectedMacroKeys = new[] { Guid.NewGuid() },
            GenerateStubPartialViews = false
        };
        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var macroMock = new Mock<IMacroConverter>();
        macroMock.Setup(c => c.ConverterName).Returns("Macro to Rich Text Block");
        macroMock.Setup(c => c.ExecuteConversionAsync(
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Macro to Rich Text Block"))
            .Returns(macroMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        macroMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o =>
                o.ConverterType == "Macro to Rich Text Block"
                && o.GenerateStubPartialViews == false),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Completed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenConverterNotFound_MarksQueueItemFailed()
    {
        var options = new ConversionOptions { ConverterType = "Nonexistent" };
        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Nonexistent"))
            .Returns((IPropertyConverter?)null);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Failed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RunTestFirst_WhenTestFails_DoesNotRunActualConversion()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            RunTestFirst = true,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        // Test run returns Failed
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.Is<ConversionOptions>(o => o.IsTestRun),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Failed,
                ErrorMessage = "Test failed"
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        // Test run was called (with IsTestRun = true)
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => o.IsTestRun),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Actual conversion was NOT called (with IsTestRun = false)
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => !o.IsTestRun),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        // Queue item marked as Failed
        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Failed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RunTestFirst_WhenTestSucceeds_RunsActualConversion()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            RunTestFirst = true,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        // Test run succeeds
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.Is<ConversionOptions>(o => o.IsTestRun),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        // Actual conversion also succeeds
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.Is<ConversionOptions>(o => !o.IsTestRun),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        // Both test and actual conversion were called
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => o.IsTestRun),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => !o.IsTestRun),
            It.IsAny<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Queue item completed with success
        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Completed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenInvalidJson_MarksQueueItemFailed()
    {
        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = "{ invalid json }}}",
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        _queueServiceMock.Verify(q => q.CompleteQueueItemAsync(
            queueItem.Id,
            ConversionStatus.Failed,
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenItemDequeued_PassesProgressReporterToConverter()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        // Verify progress reporter was created for this queue item
        _progressFactoryMock.Verify(
            f => f.Create(queueItem.Id),
            Times.Once);

        // Verify converter received a non-null progress reporter
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.IsAny<ConversionOptions>(),
            It.IsNotNull<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task WhenConversionCompletes_CallsSendCompletedAsync()
    {
        var conversionId = Guid.NewGuid();
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = conversionId,
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        _progressFactoryMock.Verify(
            f => f.SendCompletedAsync(
                queueItem.Id,
                ConversionStatus.Completed,
                conversionId),
            Times.Once);
    }

    [TestMethod]
    public async Task WhenConversionFails_CallsSendCompletedAsyncWithFailed()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.IsAny<ConversionOptions>(),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something broke"));

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        _progressFactoryMock.Verify(
            f => f.SendCompletedAsync(
                queueItem.Id,
                ConversionStatus.Failed,
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RunTestFirst_PassesProgressReporterToTestRun()
    {
        var options = new ConversionOptions
        {
            ConverterType = "Test Converter",
            IsTestRun = false,
            RunTestFirst = true,
            PerformingUserKey = Guid.NewGuid()
        };

        var queueItem = new QueueItem
        {
            Id = Guid.NewGuid(),
            SerializedOptions = JsonSerializer.Serialize(options),
            Status = ConversionStatus.Running
        };

        var callCount = 0;
        _queueServiceMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? queueItem : null);

        var converterMock = new Mock<IPropertyConverter>();
        // Test run succeeds
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.Is<ConversionOptions>(o => o.IsTestRun),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        // Actual conversion also succeeds
        converterMock.Setup(c => c.ExecuteConversionAsync(
                It.Is<ConversionOptions>(o => !o.IsTestRun),
                It.IsAny<IProgress<ConversionProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversionResult
            {
                ConversionId = Guid.NewGuid(),
                Status = ConversionStatus.Completed
            });

        _converterServiceMock.Setup(c => c.GetLegacyConverterByName("Test Converter"))
            .Returns(converterMock.Object);

        var task = new ConversionBackgroundTask(_scopeFactoryMock.Object, _loggerMock.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await task.StartAsync(cts.Token);
        await Task.Delay(500);
        await task.StopAsync(CancellationToken.None);

        // Both test run and actual conversion should receive non-null progress reporters
        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => o.IsTestRun),
            It.IsNotNull<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        converterMock.Verify(c => c.ExecuteConversionAsync(
            It.Is<ConversionOptions>(o => !o.IsTestRun),
            It.IsNotNull<IProgress<ConversionProgress>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
