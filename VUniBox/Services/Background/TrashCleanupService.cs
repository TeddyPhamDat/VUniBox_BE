using VUniBox.Services.DocumentManagement;

namespace VUniBox.Services.Background
{
    public class TrashCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrashCleanupService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24); // Chạy mỗi 24 giờ

        public TrashCleanupService(IServiceProvider serviceProvider, ILogger<TrashCleanupService> logger)
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
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var documentLifecycleService = scope.ServiceProvider.GetRequiredService<IDocumentLifecycleService>();
                        
                        _logger.LogInformation("Starting trash cleanup service...");
                        
                        var success = await documentLifecycleService.AutoCleanTrashAsync();
                        
                        if (success)
                        {
                            _logger.LogInformation("Trash cleanup completed successfully");
                        }
                        else
                        {
                            _logger.LogWarning("Trash cleanup completed with warnings");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during trash cleanup");
                }

                await Task.Delay(_period, stoppingToken);
            }
        }
    }
}








