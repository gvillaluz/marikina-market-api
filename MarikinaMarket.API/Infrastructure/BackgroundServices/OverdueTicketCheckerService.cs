using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Infrastructure.BackgroundServices
{
    public class OverdueTicketCheckerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OverdueTicketCheckerService> _logger;

        public OverdueTicketCheckerService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<OverdueTicketCheckerService> logger
        )
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Overdue Ticket Checker Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var ticketService = scope.ServiceProvider.GetRequiredService<ITicketService>();

                try
                {
                    var count = await ticketService.CheckAndNotifyOverdueTicketsAsync();

                    if (count > 0)
                        _logger.LogInformation("{Count} ticket(s) marked overdue and enforcers notified.", count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check overdue tickets.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}