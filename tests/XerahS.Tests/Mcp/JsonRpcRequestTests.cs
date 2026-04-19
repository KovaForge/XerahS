using NUnit.Framework;
using XerahS.McpServer.JsonRpc;

namespace XerahS.Tests.Mcp;

[TestFixture]
public class JsonRpcRequestTests
{
    [Test]
    public void IsNotification_ReturnsTrue_WhenIdIsMissing()
    {
        var request = new JsonRpcRequest
        {
            Method = "initialized",
            Id = null
        };

        Assert.That(request.IsNotification, Is.True);
    }

    [Test]
    public void IsNotification_ReturnsFalse_WhenIdIsPresent()
    {
        var request = new JsonRpcRequest
        {
            Method = "tools/list",
            Id = 1
        };

        Assert.That(request.IsNotification, Is.False);
    }
}
