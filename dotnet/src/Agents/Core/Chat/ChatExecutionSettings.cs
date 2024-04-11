﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SemanticKernel.Agents.Chat;

/// <summary>
/// Delegate definition for <see cref="ChatExecutionSettings.TerminationStrategy"/>.
/// </summary>
/// <param name="agent">The agent actively interacting with an <see cref="AgentGroupChat"/>.</param>
/// <param name="history">The chat history.</param>
/// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
/// <returns>True to terminate chat loop.</returns>
public delegate Task<bool> TerminationCriteriaCallback(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken);

/// <summary>
/// Delegate definition for <see cref="ChatExecutionSettings.SelectionStrategy"/>.
/// </summary>
/// <param name="agents">The agents participating in an <see cref="AgentGroupChat"/>.</param>
/// <param name="history">The chat history.</param>
/// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
/// <returns>The agent who shall take the next turn.</returns>
public delegate Task<Agent?> SelectionCriteriaCallback(IReadOnlyList<Agent> agents, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken);

/// <summary>
/// Settings that affect behavior of <see cref="AgentGroupChat"/>.
/// </summary>
/// <remarks>
/// Default behavior result in no agent selection.
/// </remarks>
public class ChatExecutionSettings
{
    /// <summary>
    /// Restrict number of turns to one, by default.
    /// </summary>
    public const int DefaultMaximumIterations = 1;

    /// <summary>
    /// The maximum number of agent interactions for a given chat invocation.
    /// </summary>
    public int MaximumIterations { get; set; } = DefaultMaximumIterations;

    /// <summary>
    /// Optional strategy for evaluating whether to terminate multiturn chat.
    /// </summary>
    /// <remarks>
    /// See <see cref="TerminationStrategy"/>.
    /// </remarks>
    public TerminationCriteriaCallback? TerminationStrategy { get; set; }

    /// <summary>
    /// An optional strategy for selecting the next agent.
    /// </summary>
    /// <remarks>
    /// See <see cref="SelectionStrategy"/>.
    /// </remarks>
    public SelectionCriteriaCallback? SelectionStrategy { get; set; }
}
