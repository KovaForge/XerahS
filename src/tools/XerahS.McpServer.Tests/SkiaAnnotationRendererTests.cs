using System.Text.Json.Nodes;
using SkiaSharp;
using XerahS.McpServer.Runtime;
using Xunit;

namespace XerahS.McpServer.Tests;

public class SkiaAnnotationRendererTests
{
    [Fact]
    public void ApplyAnnotations_CoercesScalarParametersWithoutThrowing()
    {
        using var bitmap = new SKBitmap(64, 64);
        var annotations = JsonNode.Parse("""
        [
          {
            "type": "text",
            "params": {
              "x": "8",
              "y": "24",
              "text": 12345,
              "font_size": "14",
              "color": "#ff0000"
            }
          },
          {
            "type": "step",
            "params": {
              "x": 32,
              "y": 32,
              "number": "7",
              "radius": "10"
            }
          }
        ]
        """)!.AsArray();

        var applied = SkiaAnnotationRenderer.ApplyAnnotations(bitmap, annotations);

        Assert.Equal(new[] { "text", "step" }, applied);
    }

    [Fact]
    public void ApplyAnnotations_IgnoresMalformedTypeWithoutThrowing()
    {
        using var bitmap = new SKBitmap(32, 32);
        var annotations = JsonNode.Parse("""
        [
          {
            "type": 42,
            "params": {
              "x": 1,
              "y": 1,
              "width": 8,
              "height": 8
            }
          }
        ]
        """)!.AsArray();

        var applied = SkiaAnnotationRenderer.ApplyAnnotations(bitmap, annotations);

        Assert.Empty(applied);
    }
}
