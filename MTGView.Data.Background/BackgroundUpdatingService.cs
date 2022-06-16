using MTGView.Data.Background.Interfaces;
using MTGView.Data.Background.Models;

namespace MTGView.Data.Background;

public class BackgroundUpdatingService : BackgroundService
{
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(10d);

    private readonly ILogger<BackgroundUpdatingService> _logger;

    private readonly IServiceScopeFactory _services;

    private Int32 _executionCount = 0;

    public BackgroundUpdatingService(ILogger<BackgroundUpdatingService> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _services = serviceScopeFactory;
        _logger = logger;
    }

    public Boolean IsEnabled { get; set; }

    #region Overrides
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_updateInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (!IsEnabled || _executionCount <= 0)
                {
                    _logger.LogInformation("Skipped {serviceName} Execution", nameof(BackgroundUpdatingService));

                    continue;
                }

                await ConsumeFileDownloadingServices(stoppingToken);

                await ConsumeCardReplacementServices(stoppingToken);

                await ConsumeRulingReplacementServices(stoppingToken);

                await ConsumeLegalityReplacementServices(stoppingToken);

                _executionCount++;

                _logger.LogInformation("{serviceName} Execution: {executionCount}", nameof(BackgroundUpdatingService), _executionCount);

            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to execute {serviceName} due to exception: {@ex}", nameof(BackgroundUpdatingService), ex);

            }
            finally
            {
                await CleanupDownloadedFiles(stoppingToken);
            }
        }

    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
    #endregion
    #region Cleanup
    private Task CleanupDownloadedFiles(CancellationToken stoppingToken)
    {
        const string pattern = "*.csv";

        _logger.LogInformation("Running cleanup steps at {currentTime}", DateTime.Now);

        var currentDirectory = Directory.GetCurrentDirectory();

        var matches = Directory.GetFiles(currentDirectory, pattern);

        foreach (var file in Directory.GetFiles(currentDirectory).Where(fileName => matches.Contains(fileName)))
        {
            _logger.LogInformation("Deleting {fileName} from Current Directory", file);
            File.Delete(file);
            _logger.LogInformation("Deleted {fileName} from Current Directory", file);
        }

        _logger.LogInformation("Finished cleanup steps at {currentTime}", DateTime.Now);

        return Task.CompletedTask;
    }
    #endregion
    #region Scoped Service Calls
    private async Task ConsumeFileDownloadingServices(CancellationToken stoppingToken)
    {
        await using var scope = _services.CreateAsyncScope();

        var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IUnzippingService>();

        await scopedProcessingService.InitiateFileDownloadProcess(stoppingToken);
    }

    private async Task ConsumeCardReplacementServices(CancellationToken stoppingToken)
    {
        await using var scope = _services.CreateAsyncScope();

        var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IReplaceCardsService>();

        _logger.LogInformation("Processing started at {timeStarted}", DateTime.Now);

        await scopedProcessingService.DeserializeCsvToMagicCards($"{FileNamesToProcess.Cards}", stoppingToken);

        _logger.LogInformation("Processing finished at {timeStarted}", DateTime.Now);
    }

    private async Task ConsumeRulingReplacementServices(CancellationToken stoppingToken)
    {
        await using var scope = _services.CreateAsyncScope();

        var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IReplaceRulingsService>();

        _logger.LogInformation("Processing started at {timeStarted}", DateTime.Now);

        await scopedProcessingService.DeserializeCsvToRulings($"{FileNamesToProcess.Rulings}", stoppingToken);

        _logger.LogInformation("Processing finished at {timeStarted}", DateTime.Now);
    }

    private async Task ConsumeLegalityReplacementServices(CancellationToken stoppingToken)
    {
        await using var scope = _services.CreateAsyncScope();

        var scopedProcessingService = scope.ServiceProvider.GetRequiredService<IReplaceLegalitiesService>();

        _logger.LogInformation("Processing started at {timeStarted}", DateTime.Now);

        await scopedProcessingService.DeserializeCsvToLegalities($"{FileNamesToProcess.Legalities}", stoppingToken);

        _logger.LogInformation("Processing finished at {timeStarted}", DateTime.Now);
    }
    #endregion
}