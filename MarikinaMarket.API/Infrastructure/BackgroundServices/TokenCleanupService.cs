using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Infrastructure.BackgroundServices
{
    public class TokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenCleanupService> _logger;

        public TokenCleanupService(IServiceProvider serviceProvider, ILogger<TokenCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IAuthService>().CleanupTokensAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning("Token cleanup failed ({ExceptionType}).", ex.GetType().Name);
                }

                try { await Task.Delay(TimeSpan.FromDays(1), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
