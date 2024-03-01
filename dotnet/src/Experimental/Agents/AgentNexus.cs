﻿// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Experimental.Agents;

/// <summary>
/// $$$
/// </summary>
public abstract class AgentNexus /*: $$$ ChatHistory ??? */
{
    private readonly Dictionary<Type, AgentChannel> _agentChannels;
    private readonly ChatHistory _agentHistory;

    /// <summary>
    /// $$$
    /// </summary>
    public IReadOnlyList<ChatMessageContent> Messages => this._agentHistory;

    internal ChatHistory AgentHistory => this._agentHistory; // $$$

    /// <summary>
    /// $$$
    /// </summary>
    /// <param name="agent"></param>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected Task<IEnumerable<ChatMessageContent>> InvokeAgentAsync(KernelAgent agent, string? input = null, /*KernelArguments $$$,*/ CancellationToken cancellationToken = default)
    {
        var content = string.IsNullOrWhiteSpace(input) ? null : new ChatMessageContent(AuthorRole.User, input);
        return this.InvokeAgentAsync(agent, content, cancellationToken);
    }

    /// <summary>
    /// $$$
    /// </summary>
    /// <param name="agent"></param>
    /// <param name="input"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected async Task<IEnumerable<ChatMessageContent>> InvokeAgentAsync(KernelAgent agent, ChatMessageContent? input = null, /*KernelArguments $$$,*/ CancellationToken cancellationToken = default)
    {
        // Manifest the required channel
        var channel = await this.GetChannelAsync(agent, cancellationToken).ConfigureAwait(false);

        // Invoke agent
        var response = await channel.InvokeAsync(agent, input, cancellationToken).ConfigureAwait(false);

        // Process response
        foreach (var message in response)
        {
            this.AgentHistory.Add(message);

            var tasks =
                this._agentChannels.Values
                    .Where(c => c != channel)
                    .Select(c => c.RecieveAsync([message]));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        return response;
    }

    private async Task<AgentChannel> GetChannelAsync(KernelAgent agent, CancellationToken cancellationToken)
    {
        // $$$ BETTER KEY THAN TYPE (DIFFERENT APIKEYS / ENDPOINTS / TENENTS OF SAME TYPE)
        if (!this._agentChannels.TryGetValue(agent.ChannelType, out var channel))
        {
            // $$$ CHANNEL FACTORY (CREATE / RESTORE) - CONTEXT
            channel = (AgentChannel)(Activator.CreateInstance(agent.ChannelType) ?? throw new InvalidOperationException("$$$"));

            channel.Init(this); // $$$ CONTEXT ???

            await channel.RecieveAsync(this.Messages, cancellationToken).ConfigureAwait(false); // $$$ NEED SYNC-POINT FOR RESTORE

            this._agentChannels[agent.ChannelType] = channel;
        }

        return channel;
    }

    /// <summary>
    /// $$$
    /// </summary>
    protected AgentNexus()
    {
        this._agentChannels = [];
        this._agentHistory = [];
    }
}
