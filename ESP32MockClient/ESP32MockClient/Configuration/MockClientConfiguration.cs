namespace ESP32MockClient.Configuration;

public static class MockClientConfiguration
{
    //the stremed resolution in which the photos will be sent from the mock client
    public static string Resolution { get; set; } = "2048x1536";

    public static int TargetFps { get; } = 15;

    public static int ServerHttpsPort { get; } = 7010;

    public static int ServerTcpPort { get; } = 7000;

    public static string FootagePath { get; } = "footage";

    public static string FfmpegPath { get; } = "ffmpeg";

    public static int FrameDelayMs => 1000 / TargetFps;

    public static int MaxFailedConnectionAttempts { get; } = 50;
}
