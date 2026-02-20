using System.ComponentModel;
using System.Text.Json;
using Mcp.Common.Core;
using CodeAssist.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CodeAssistMcp.McpTools;

/// <summary>
/// MCP tools for serving personal context (voice profile, etc.).
/// </summary>
[McpServerToolType]
public class PersonalContextTools(
    IOptions<CodeAssistOptions> options,
    ILogger<PersonalContextTools> logger)
{
    private readonly CodeAssistOptions _options = options.Value;

    [McpServerTool, DisplayName("get_voice_profile")]
    [Description("Returns the user's writing voice profile — tone, sentence patterns, vocabulary preferences, and stylistic habits. Use this before generating any prose on the user's behalf (emails, docs, commit messages, cover letters, etc.) to match their natural voice.")]
    public async Task<string> GetVoiceProfile()
    {
        try
        {
            if (string.IsNullOrEmpty(_options.VoiceProfilePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "VoiceProfilePath is not configured in appsettings.json"
                }, SerializerOptions.JsonOptionsIndented);
            }

            if (!File.Exists(_options.VoiceProfilePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Voice profile not found at: {_options.VoiceProfilePath}"
                }, SerializerOptions.JsonOptionsIndented);
            }

            string content = await File.ReadAllTextAsync(_options.VoiceProfilePath);

            logger.LogDebug("Served voice profile from {Path} ({Length} chars)",
                _options.VoiceProfilePath, content.Length);

            return JsonSerializer.Serialize(new
            {
                success = true,
                profile = content,
                sourcePath = _options.VoiceProfilePath
            }, SerializerOptions.JsonOptionsIndented);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading voice profile from {Path}", _options.VoiceProfilePath);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, SerializerOptions.JsonOptionsIndented);
        }
    }
}
