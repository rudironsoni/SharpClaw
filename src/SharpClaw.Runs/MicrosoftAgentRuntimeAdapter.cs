// <copyright file="MicrosoftAgentRuntimeAdapter.cs" company="SharpClaw">
// Licensed under the MIT License. See LICENSE file.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SharpClaw.Abstractions;
using System.Text.Json;

namespace SharpClaw.Runs;

/// <summary>
/// Adapter that bridges SharpClaw runs with Microsoft Agent Framework.
/// Configures and executes AI agents with event streaming support.
/// </summary>
public sealed class MicrosoftAgentRuntimeAdapter : IAgentRuntimeAdapter
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<MicrosoftAgentRuntimeAdapter>? _logger;

    /// <summary>
    /// Event types for agent execution.
    /// </summary>
    public static class EventTypes
    {
        /// <summary>Agent step started.</summary>
        public const string StepStart = "agent.step_start";

        /// <summary>Agent step completed.</summary>
        public const string StepComplete = "agent.step_complete";

        /// <summary>Tool call requested.</summary>
        public const string ToolCall = "agent.tool_call";

        /// <summary>Tool result received.</summary>
        public const string ToolResult = "agent.tool_result";

        /// <summary>Text delta received.</summary>
        public const string TextDelta = "agent.text_delta";

        /// <summary>Agent execution completed.</summary>
        public const string Completed = "agent.completed";

        /// <summary>Agent execution failed.</summary>
        public const string Failed = "agent.failed";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrosoftAgentRuntimeAdapter"/> class.
    /// </summary>
    /// <param name="chatClient">The chat client for AI interactions.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public MicrosoftAgentRuntimeAdapter(IChatClient chatClient, ILogger<MicrosoftAgentRuntimeAdapter>? logger = null)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RunResult> ExecuteAsync(RunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await PublishEventAsync(request, EventTypes.StepStart, "Agent initialized").ConfigureAwait(false);

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.User, request.Input)
            };

            var options = new ChatOptions
            {
                Tools = GetAvailableTools()
            };

            await PublishEventAsync(request, EventTypes.StepComplete, "Processing input...").ConfigureAwait(false);

            // Stream the response
            var responseBuilder = new System.Text.StringBuilder();

            await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Handle text updates
                if (update.Text is { Length: > 0 } textDelta)
                {
                    responseBuilder.Append(textDelta);
                    await PublishEventAsync(request, EventTypes.TextDelta, textDelta).ConfigureAwait(false);
                }

                // Handle function calls/tool calls
                if (update.Contents is { Count: > 0 } contents)
                {
                    foreach (var content in contents)
                    {
                        if (content is FunctionCallContent functionCall)
                        {
                            var toolCallData = JsonSerializer.Serialize(new
                            {
                                tool = functionCall.Name,
                                arguments = functionCall.Arguments
                            });
                            await PublishEventAsync(request, EventTypes.ToolCall, toolCallData).ConfigureAwait(false);
                        }

                        if (content is FunctionResultContent functionResult)
                        {
                            var toolResultData = JsonSerializer.Serialize(new
                            {
                                callId = functionResult.CallId,
                                result = functionResult.Result?.ToString()
                            });
                            await PublishEventAsync(request, EventTypes.ToolResult, toolResultData).ConfigureAwait(false);
                        }
                    }
                }
            }

            var finalResponse = responseBuilder.ToString();
            await PublishEventAsync(request, EventTypes.Completed, finalResponse).ConfigureAwait(false);

            return new RunResult(request.RunId, OperationResult.Success());
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Run {RunId} was cancelled", request.RunId);
            await PublishEventAsync(request, EventTypes.Failed, "Execution was cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing run {RunId}", request.RunId);
            await PublishEventAsync(request, EventTypes.Failed, ex.Message).ConfigureAwait(false);
            return new RunResult(request.RunId, OperationResult.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Publishes an event through the request callback.
    /// </summary>
    private static async Task PublishEventAsync(RunRequest request, string eventType, string? data)
    {
        if (request.OnEvent is null)
        {
            return;
        }

        try
        {
            await request.OnEvent(eventType, data).ConfigureAwait(false);
        }
        catch
        {
            // Event publishing errors should not fail the execution
        }
    }

    /// <summary>
    /// Gets the available tools for the agent.
    /// </summary>
    private static IList<AITool> GetAvailableTools()
    {
        // For now, return empty list - tools can be registered later
        return [];
    }
}
