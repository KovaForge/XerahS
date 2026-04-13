#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XerahS.Common;
using XerahS.Assistant.Configuration;
using XerahS.Assistant.Models;

namespace XerahS.Assistant.Providers;

public interface IAssistantModelProvider
{
    AssistantProviderMetadata Metadata { get; }
    Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken);
    Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken);
}

public static class AssistantModelProviderFactory
{
    public static IAssistantModelProvider Create(AssistantProviderRuntimeSettings settings) => new HttpAssistantModelProvider(settings);
}

internal sealed class HttpAssistantModelProvider : IAssistantModelProvider
{
    private readonly AssistantProviderRuntimeSettings _settings;

    public HttpAssistantModelProvider(AssistantProviderRuntimeSettings settings)
    {
        _settings = settings;
    }

    public AssistantProviderMetadata Metadata => _settings.Metadata;

    public Task<AssistantModelResult> ValidateAsync(string modelId, CancellationToken cancellationToken)
    {
        AssistantModelRequest request = new(
            _settings.Metadata.Id,
            modelId,
            [
                new AssistantMessage(AssistantModelMessageRole.System, "Reply with ok."),
                new AssistantMessage(AssistantModelMessageRole.User, "ok")
            ],
            [],
            AssistantPrivacyScope.CloudText,
            AllowImageContent: false);

        return CompleteAsync(request, cancellationToken);
    }

    public async Task<AssistantModelResult> CompleteAsync(AssistantModelRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage message = BuildRequest(request);
            HttpClient client = HttpClientFactory.Create();
            using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Error($"Provider returned {(int)response.StatusCode}: {TrimForStatus(responseText)}");
            }

            string? text = ExtractText(responseText);
            return string.IsNullOrWhiteSpace(text)
                ? Error("Provider returned an empty response.")
                : new AssistantModelResult(AssistantModelResultKind.Text, text.Trim(), [], null, TryGetRequestId(response));
        }
        catch (OperationCanceledException)
        {
            return new AssistantModelResult(AssistantModelResultKind.Cancelled, null, [], null, null);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, $"Assistant provider request failed: {_settings.Metadata.Id}");
            return Error("Provider request failed.");
        }
    }

    private HttpRequestMessage BuildRequest(AssistantModelRequest request)
    {
        return _settings.Metadata.Protocol switch
        {
            AssistantProviderProtocol.OpenAiResponses => BuildOpenAiResponsesRequest(request),
            AssistantProviderProtocol.OpenAiCompatibleChat => BuildOpenAiCompatibleChatRequest(request),
            AssistantProviderProtocol.GeminiGenerateContent => BuildGeminiRequest(request),
            AssistantProviderProtocol.AnthropicMessages => BuildAnthropicRequest(request),
            AssistantProviderProtocol.OllamaGenerate => BuildOllamaRequest(request),
            _ => throw new NotSupportedException($"Unsupported assistant provider: {_settings.Metadata.Protocol}")
        };
    }

    private HttpRequestMessage BuildOpenAiResponsesRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            input = string.Join("\n\n", request.Messages.Select(message => $"{message.Role}: {message.Text}")),
            max_output_tokens = 80
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/responses", payload, bearerToken: _settings.ApiKey);
    }

    private HttpRequestMessage BuildOpenAiCompatibleChatRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            messages = request.Messages.Select(message => new
            {
                role = message.Role.ToString().ToLowerInvariant(),
                content = message.Text
            }),
            max_tokens = 80,
            temperature = 0
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/chat/completions", payload, bearerToken: _settings.ApiKey);
    }

    private HttpRequestMessage BuildGeminiRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            contents = request.Messages
                .Where(message => message.Role != AssistantModelMessageRole.System)
                .Select(message => new
                {
                    role = message.Role == AssistantModelMessageRole.User ? "user" : "model",
                    parts = new[] { new { text = message.Text } }
                }),
            systemInstruction = new
            {
                parts = request.Messages
                    .Where(message => message.Role == AssistantModelMessageRole.System)
                    .Select(message => new { text = message.Text })
            },
            generationConfig = new
            {
                maxOutputTokens = 80,
                temperature = 0
            }
        };

        string url = $"{_settings.BaseUrl}/models/{Uri.EscapeDataString(request.ModelId)}:generateContent?key={Uri.EscapeDataString(_settings.ApiKey ?? string.Empty)}";
        return BuildJsonRequest(HttpMethod.Post, url, payload);
    }

    private HttpRequestMessage BuildAnthropicRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            max_tokens = 80,
            temperature = 0,
            system = string.Join("\n", request.Messages.Where(message => message.Role == AssistantModelMessageRole.System).Select(message => message.Text)),
            messages = request.Messages
                .Where(message => message.Role != AssistantModelMessageRole.System)
                .Select(message => new
                {
                    role = message.Role == AssistantModelMessageRole.User ? "user" : "assistant",
                    content = message.Text
                })
        };

        HttpRequestMessage message = BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/messages", payload);
        message.Headers.Add("x-api-key", _settings.ApiKey ?? string.Empty);
        message.Headers.Add("anthropic-version", "2023-06-01");
        return message;
    }

    private HttpRequestMessage BuildOllamaRequest(AssistantModelRequest request)
    {
        var payload = new
        {
            model = request.ModelId,
            prompt = string.Join("\n\n", request.Messages.Select(message => $"{message.Role}: {message.Text}")),
            stream = false,
            options = new
            {
                temperature = 0
            }
        };

        return BuildJsonRequest(HttpMethod.Post, $"{_settings.BaseUrl}/api/generate", payload);
    }

    private static HttpRequestMessage BuildJsonRequest(HttpMethod method, string url, object payload, string? bearerToken = null)
    {
        var message = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return message;
    }

    private string? ExtractText(string responseText)
    {
        using JsonDocument document = JsonDocument.Parse(responseText);
        JsonElement root = document.RootElement;

        return _settings.Metadata.Protocol switch
        {
            AssistantProviderProtocol.OpenAiResponses => TryGetString(root, "output_text") ?? ExtractOpenAiOutputText(root),
            AssistantProviderProtocol.OpenAiCompatibleChat => ExtractPath(root, "choices", 0, "message", "content"),
            AssistantProviderProtocol.GeminiGenerateContent => ExtractPath(root, "candidates", 0, "content", "parts", 0, "text"),
            AssistantProviderProtocol.AnthropicMessages => ExtractPath(root, "content", 0, "text"),
            AssistantProviderProtocol.OllamaGenerate => TryGetString(root, "response"),
            _ => null
        };
    }

    private static string? ExtractOpenAiOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out JsonElement output) || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement contentItem in content.EnumerateArray())
            {
                string? text = TryGetString(contentItem, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? ExtractPath(JsonElement root, params object[] segments)
    {
        JsonElement current = root;
        foreach (object segment in segments)
        {
            if (segment is string property)
            {
                if (!current.TryGetProperty(property, out current))
                {
                    return null;
                }
            }
            else if (segment is int index)
            {
                if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= index)
                {
                    return null;
                }

                current = current[index];
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? TryGetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string TrimForStatus(string value)
    {
        value = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return value.Length <= 160 ? value : value[..157] + "...";
    }

    private static string? TryGetRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("x-request-id", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static AssistantModelResult Error(string message) =>
        new(AssistantModelResultKind.Error, message, [], null, null);
}
