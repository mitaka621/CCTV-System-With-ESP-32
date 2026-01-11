namespace ESP32MockClient.Configuration;

public static class MockClientConfiguration
{
    //the stremed resolution in which the photos will be sent from the mock client
    public static string Resolution { get; set; } = "1280x720";

    public static int TargetFps { get; } = 15;

    //same as the server side secret
    public static string SharedSecretCamKey { get; } = "9GDBevtlBaWVgfzSUbdT1uIgbOyskzHgCp4EuwARdqVUVcuvqBXpF16RyU06buS2sjZ3Ko40Ys1kC6J33vpxDId35keai7F7D1JXa0d9l8y5XHy5zyjYcaaDjUWjogf0HK9Q3IfcgYri7fVBJllSrlw05D2dsf5BFmcwOHCvmGW3UFf";

    public static int ServerHttpsPort { get; } = 7010;

    public static int ServerTcpPort { get; } = 7000;

    public static int LocalHttpPort { get; } = 77;

    public static string MacAddress { get; } = "AA:BB:CC:DD:EE:FF";

    public static string FootagePath { get; } = "footage";

    public static string FfmpegPath { get; } = "ffmpeg";

    public static int FrameDelayMs => 1000 / TargetFps;

    public static int MaxFailedConnectionAttempts { get; } = 50;

    public static int MaxFailedPairingAttempts { get; } = 20;

    public static int MaxFailedFrameSends { get; } = 10;

    //will be automatically set when handshake is initiated
    public static string? ServerAddress { get; set; }

    //will be automatically set when handshake is initiated
    public static string? BaseServerUrl { get; set; }

    //will be automatically set when handshake is completed
    public static string? SessionToken { get; set; }
}
