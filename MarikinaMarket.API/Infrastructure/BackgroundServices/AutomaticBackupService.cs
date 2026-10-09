using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Infrastructure.BackgroundServices
{
    public class AutomaticBackupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutomaticBackupService> _logger;

        public AutomaticBackupService(IServiceProvider serviceProvider, ILogger<AutomaticBackupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var nextCleanup = DateTime.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var service = scope.ServiceProvider.GetRequiredService<IBackupService>();
                        await service.InitializeAsync(stoppingToken);
                        await service.RunScheduledAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    // Never log provider exception messages, secrets, or pg_dump diagnostics.
                    _logger.LogWarning("Automatic backup check failed ({ExceptionType}).", ex.GetType().Name);
                }

                if (DateTime.UtcNow >= nextCleanup)
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IBackupService>().CleanupAsync(stoppingToken);
                        nextCleanup = DateTime.UtcNow.AddHours(1);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Backup maintenance will be retried ({ExceptionType}).", ex.GetType().Name);
                    }
                }
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
