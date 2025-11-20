namespace GameSocketServer;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;
    private SocketServer? _server;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Socket Server Starting...");

        _server = new SocketServer(5000);

        await _server.StartAsync();

        // StartAsync 내부에서 무한 루프 → 실행 지속
    }
}
