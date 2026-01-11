using ESP32MockClient;
using ESP32MockClient.Configuration;

Console.Title = "ESP32-CAM Mock Client";

Console.WriteLine("========================================");
Console.WriteLine("  ESP32-CAM Mock Client");
Console.WriteLine("========================================");
Console.WriteLine($"  MAC: {MockClientConfiguration.MacAddress}");
Console.WriteLine($"  HTTP Port: {MockClientConfiguration.LocalHttpPort}");
Console.WriteLine($"  Target FPS: {MockClientConfiguration.TargetFps}");
Console.WriteLine("========================================");
Console.WriteLine();
Console.WriteLine("  HINT: Place MP4 video files in the 'footage' folder");
Console.WriteLine("        then restart the app to stream them.");
Console.WriteLine();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    Console.WriteLine("\nShutdown requested...");
    e.Cancel = true;
    cts.Cancel();
};

using var client = new MockEsp32Client();

try
{
    await client.RunAsync(cts.Token);
}
catch (OperationCanceledException) { }
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("Goodbye!");
