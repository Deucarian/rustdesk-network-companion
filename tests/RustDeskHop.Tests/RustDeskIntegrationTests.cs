using System.Net;
using System.Net.Sockets;
using Simultria.RustDeskCompanion;
using Xunit;

namespace RustDeskHop.Tests;

public sealed class RustDeskIntegrationTests
{
    [Fact]
    public void BuildsPublicConnectionTarget()
    {
        var target = new TargetDefinition { RustDeskId = "123456789" };
        var profile = new ServerProfile { ServerAddress = "public" };

        Assert.Equal("123456789@public", ConnectionTargetBuilder.Build(target, profile));
    }

    [Fact]
    public void BuildsPrivateConnectionTargetWithKey()
    {
        var target = new TargetDefinition { RustDeskId = "123456789" };
        var profile = new ServerProfile
        {
            ServerAddress = "rustdesk.example:21116",
            PublicKey = "abc+/=",
        };

        Assert.Equal(
            "123456789@rustdesk.example:21116?key=abc+/=",
            ConnectionTargetBuilder.Build(target, profile));
    }

    [Theory]
    [InlineData("123@public")]
    [InlineData("123?key=bad")]
    [InlineData("123&bad")]
    public void RejectsUnsafeRustDeskIds(string id)
    {
        var target = new TargetDefinition { RustDeskId = id };
        var profile = new ServerProfile { ServerAddress = "public" };

        Assert.Throws<InvalidOperationException>(() => ConnectionTargetBuilder.Build(target, profile));
    }

    [Fact]
    public void EffectiveRendezvousServerWinsOverStaleCustomOption()
    {
        var path = WriteTemporaryFile("""
            rendezvous_server = 'rs-ny.rustdesk.com:21116'

            [options]
            custom-rendezvous-server = 'private.example:21116'
            """);

        try
        {
            Assert.Equal("rs-ny.rustdesk.com:21116", RustDeskConfigReader.ReadConfiguredServer(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CustomServerIsUsedWhenEffectiveServerIsMissing()
    {
        var path = WriteTemporaryFile("""
            [options]
            custom-rendezvous-server = 'private.example:21116'
            """);

        try
        {
            Assert.Equal("private.example:21116", RustDeskConfigReader.ReadConfiguredServer(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DetectsAStoredRustDeskLoginWithoutReadingItOutsideTheProcess()
    {
        var path = WriteTemporaryFile("access_token = 'encrypted-token-value'");

        try
        {
            Assert.True(RustDeskAccountState.HasLoginToken(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EmptyLoginTokenIsNotTreatedAsSignedIn()
    {
        var path = WriteTemporaryFile("access_token = ''");

        try
        {
            Assert.False(RustDeskAccountState.HasLoginToken(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PublicPreparationRemovesOnlyServerSpecificTomlValues()
    {
        var original = """
            rendezvous_server = 'private.example:21116'
            theme = 'dark'

            [options]
            custom-rendezvous-server = 'private.example:21116'
            relay-server = 'private.example:21117'
            api-server = 'https://private.example'
            key = 'public-key'
            enable-file-transfer = 'Y'
            """;

        var updated = RustDeskTomlEditor.ClearCustomServer(original);

        Assert.DoesNotContain("private.example", updated);
        Assert.DoesNotContain("key =", updated);
        Assert.Contains("theme = 'dark'", updated);
        Assert.Contains("enable-file-transfer = 'Y'", updated);
    }

    [Fact]
    public void ReadsServerOptionForRollback()
    {
        const string contents = """
            [options]
            custom-rendezvous-server = 'private.example:21116'
            key = "abc+/="
            """;

        Assert.Equal("private.example:21116", RustDeskTomlEditor.ReadSetting(contents, "custom-rendezvous-server"));
        Assert.Equal("abc+/=", RustDeskTomlEditor.ReadSetting(contents, "key"));
        Assert.Null(RustDeskTomlEditor.ReadSetting(contents, "api-server"));
    }

    [Fact]
    public async Task PrivateNetworkProbeConnectsToReachableServer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        try
        {
            var profile = new ServerProfile
            {
                RequiresPrivateNetwork = true,
                ProbeHost = IPAddress.Loopback.ToString(),
                ProbePort = port,
            };

            Assert.True(await NetworkProbe.CanReachAsync(profile));
            using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string WriteTemporaryFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"RustDeskHop-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, contents);
        return path;
    }
}
