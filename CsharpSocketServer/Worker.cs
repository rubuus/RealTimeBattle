namespace CsharpSocketServer;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private readonly ILogger<Worker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Socket Server Starting...");

        SocketServer _server = new SocketServer(5000);

        await _server.StartServer();
    }
}
