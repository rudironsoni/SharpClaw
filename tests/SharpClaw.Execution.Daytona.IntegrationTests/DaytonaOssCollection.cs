using SharpClaw.TestCommon;
using Xunit;

namespace SharpClaw.Execution.Daytona.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class DaytonaOssCollection : ICollectionFixture<DaytonaOssContainerFixture>
{
    public const string Name = "DaytonaOSS";
}
