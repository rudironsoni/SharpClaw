namespace SharpClaw.TestCommon;

/// <summary>
/// Common constants used across test projects.
/// </summary>
public static class TestConstants
{
    public static class Timeouts
    {
        public static readonly TimeSpan Short = TimeSpan.FromMilliseconds(100);
        public static readonly TimeSpan Medium = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan Long = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan VeryLong = TimeSpan.FromSeconds(30);
    }

    public static class Categories
    {
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string EndToEnd = "EndToEnd";
        public const string Load = "Load";
        public const string Flaky = "Flaky";
    }

    public static class Scopes
    {
        public const string Read = "operator:read";
        public const string Write = "operator:write";
        public const string Admin = "operator:admin";
    }

    public static class GatewayMethods
    {
        public const string Ping = "ping";
        public const string ChatSend = "chat.send";
        public const string ChatAbort = "chat.abort";
        public const string ChatStatus = "chat.status";
    }
}

/// <summary>
/// Trait attribute constants for xUnit.
/// </summary>
public static class TestTraits
{
    public const string Category = "Category";
    public const string Timeout = "Timeout";
}
