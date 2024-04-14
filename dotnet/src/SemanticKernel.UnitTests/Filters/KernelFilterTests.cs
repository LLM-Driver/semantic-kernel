﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextGeneration;
using Moq;
using Xunit;

namespace SemanticKernel.UnitTests.Filters;

public class KernelFilterTests
{
    [Fact]
    public async Task FunctionFilterIsTriggeredAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var preFunctionInvocations = 0;
        var postFunctionInvocations = 0;

        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            preFunctionInvocations++;
            await next(context);
            postFunctionInvocations++;
        });

        // Act
        await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(1, preFunctionInvocations);
        Assert.Equal(1, postFunctionInvocations);
    }

    [Fact]
    public async Task FunctionFilterContextHasResultAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            Assert.Null(context.Result.Value);

            await next(context);

            Assert.NotNull(context.Result);
            Assert.Equal("Result", context.Result.ToString());
        });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("Result", result.ToString());
    }

    [Fact]
    public async Task PreInvocationFunctionFilterChangesArgumentAsync()
    {
        // Arrange
        const string OriginalInput = "OriginalInput";
        const string NewInput = "NewInput";

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            context.Arguments["originalInput"] = NewInput;
            await next(context);
        });

        var function = KernelFunctionFactory.CreateFromMethod((string originalInput) => originalInput);

        // Act
        var result = await kernel.InvokeAsync(function, new() { ["originalInput"] = OriginalInput });

        // Assert
        Assert.Equal(NewInput, result.GetValue<string>());
    }

    [Fact]
    public async Task FunctionFilterCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        // Act
        await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, filterInvocations);
        Assert.Equal(0, functionInvocations);
    }

    [Fact]
    public async Task FunctionFilterCancellationWorksCorrectlyOnStreamingAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
            functionInvocations++;
        }

        // Assert
        Assert.Equal(1, filterInvocations);
        Assert.Equal(0, functionInvocations);
    }

    [Fact]
    public async Task FunctionFiltersWithPromptsWorkCorrectlyAsync()
    {
        // Arrange
        var preFunctionInvocations = 0;
        var postFunctionInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onFunctionInvocation: async (context, next) =>
            {
                preFunctionInvocations++;
                await next(context);
                postFunctionInvocations++;
            });

        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, preFunctionInvocations);
        Assert.Equal(1, postFunctionInvocations);
        mockTextGeneration.Verify(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
    }

    [Fact]
    public async Task PromptFiltersAreNotTriggeredForMethodsAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;

        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var kernel = this.GetKernelWithFilters(
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, functionInvocations);
        Assert.Equal(0, preFilterInvocations);
        Assert.Equal(0, postFilterInvocations);
    }

    [Fact]
    public async Task PromptFiltersAreTriggeredForPromptsAsync()
    {
        // Arrange
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(1, preFilterInvocations);
        Assert.Equal(1, postFilterInvocations);
    }

    [Fact]
    public async Task PromptFiltersAreTriggeredForPromptsStreamingAsync()
    {
        // Arrange
        var preFilterInvocations = 0;
        var postFilterInvocations = 0;
        var mockTextGeneration = this.GetMockTextGeneration();

        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendering: (context) =>
            {
                preFilterInvocations++;
            },
            onPromptRendered: (context) =>
            {
                postFilterInvocations++;
            });

        // Act
        await foreach (var chunk in kernel.InvokeStreamingAsync(function))
        {
        }

        // Assert
        Assert.Equal(1, preFilterInvocations);
        Assert.Equal(1, postFilterInvocations);
    }

    [Fact]
    public async Task PostInvocationPromptFilterChangesRenderedPromptAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendered: (context) =>
            {
                context.RenderedPrompt += " - updated from filter";
            });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        mockTextGeneration.Verify(m => m.GetTextContentsAsync("Prompt - updated from filter", It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task PostInvocationPromptFilterCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onPromptRendered: (context) =>
            {
                context.Cancel = true;
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelFunctionCanceledException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.Same(function, exception.Function);
        Assert.Same(kernel, exception.Kernel);
        Assert.Null(exception.FunctionResult?.GetValue<object>());
    }

    [Fact]
    public async Task FunctionAndPromptFiltersAreExecutedInCorrectOrderAsync()
    {
        // Arrange
        var builder = Kernel.CreateBuilder();
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");

        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter1-Invoked");
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter2-Invoked");
        });

        var promptFilter1 = new FakePromptFilter(
            (context) => executionOrder.Add("PromptFilter1-Rendering"),
            (context) => executionOrder.Add("PromptFilter1-Rendered"));

        var promptFilter2 = new FakePromptFilter(
            (context) => executionOrder.Add("PromptFilter2-Rendering"),
            (context) => executionOrder.Add("PromptFilter2-Rendered"));

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        builder.Services.AddSingleton<IPromptFilter>(promptFilter1);
        builder.Services.AddSingleton<IPromptFilter>(promptFilter2);

        builder.Services.AddSingleton<ITextGenerationService>(mockTextGeneration.Object);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[1]);
        Assert.Equal("PromptFilter1-Rendering", executionOrder[2]);
        Assert.Equal("PromptFilter2-Rendering", executionOrder[3]);
        Assert.Equal("PromptFilter1-Rendered", executionOrder[4]);
        Assert.Equal("PromptFilter2-Rendered", executionOrder[5]);
        Assert.Equal("FunctionFilter2-Invoked", executionOrder[6]);
        Assert.Equal("FunctionFilter1-Invoked", executionOrder[7]);
    }

    [Fact]
    public async Task MultipleFunctionFiltersCancellationWorksCorrectlyAsync()
    {
        // Arrange
        var functionInvocations = 0;
        var filterInvocations = 0;
        var function = KernelFunctionFactory.CreateFromMethod(() => functionInvocations++);

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: (context, next) =>
        {
            filterInvocations++;
            // next(context) is not called here, function invocation is cancelled.
            return Task.CompletedTask;
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(0, functionInvocations);
        Assert.Equal(1, filterInvocations);
    }

    [Fact]
    public async Task DifferentWaysOfAddingFunctionFiltersWorkCorrectlyAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");
        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
        });

        var functionFilter2 = new FakeFunctionFilter(async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
        });

        var builder = Kernel.CreateBuilder();

        // Act

        // Case #1 - Add filter to services
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);

        var kernel = builder.Build();

        // Case #2 - Add filter to kernel
        kernel.FunctionFilters.Add(functionFilter2);

        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[1]);
    }

    [Fact]
    public async Task DifferentWaysOfAddingPromptFiltersWorkCorrectlyAsync()
    {
        // Arrange
        var mockTextGeneration = this.GetMockTextGeneration();
        var function = KernelFunctionFactory.CreateFromPrompt("Prompt");
        var executionOrder = new List<string>();

        var promptFilter1 = new FakePromptFilter((context) => executionOrder.Add("PromptFilter1-Rendering"));
        var promptFilter2 = new FakePromptFilter((context) => executionOrder.Add("PromptFilter2-Rendering"));

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<ITextGenerationService>(mockTextGeneration.Object);

        // Act
        // Case #1 - Add filter to services
        builder.Services.AddSingleton<IPromptFilter>(promptFilter1);

        var kernel = builder.Build();

        // Case #2 - Add filter to kernel
        kernel.PromptFilters.Add(promptFilter2);

        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("PromptFilter1-Rendering", executionOrder[0]);
        Assert.Equal("PromptFilter2-Rendering", executionOrder[1]);
    }

    [Fact]
    public async Task InsertFilterInMiddleOfPipelineTriggersFiltersInCorrectOrderAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => "Result");
        var executionOrder = new List<string>();

        var functionFilter1 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter1-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter1-Invoked");
        });

        var functionFilter2 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter2-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter2-Invoked");
        });

        var functionFilter3 = new FakeFunctionFilter(onFunctionInvocation: async (context, next) =>
        {
            executionOrder.Add("FunctionFilter3-Invoking");
            await next(context);
            executionOrder.Add("FunctionFilter3-Invoked");
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);

        var kernel = builder.Build();

        kernel.FunctionFilters.Insert(1, functionFilter3);

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("FunctionFilter1-Invoking", executionOrder[0]);
        Assert.Equal("FunctionFilter3-Invoking", executionOrder[1]);
        Assert.Equal("FunctionFilter2-Invoking", executionOrder[2]);
        Assert.Equal("FunctionFilter2-Invoked", executionOrder[3]);
        Assert.Equal("FunctionFilter3-Invoked", executionOrder[4]);
        Assert.Equal("FunctionFilter1-Invoked", executionOrder[5]);
    }

    [Fact]
    public async Task FunctionFilterPropagatesExceptionToCallerAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException(); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                // Exception will occur here.
                // Because it's not handled, it will be propagated to the caller.
                await next(context);
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task FunctionFilterPropagatesExceptionToCallerOnStreamingAsync()
    {
        // Arrange
        static async IAsyncEnumerable<int> GetData()
        {
            await Task.Delay(0);
            yield return 1;
            throw new KernelException();
        }

        var function = KernelFunctionFactory.CreateFromMethod(GetData);

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                // Exception will occur here.
                // Because it's not handled, it will be propagated to the caller.
                await next(context);
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(async () =>
        {
            await foreach (var item in kernel.InvokeStreamingAsync<int>(function))
            { }
        });

        // Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public async Task FunctionFilterCanHandleExceptionAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new NotImplementedException(); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (NotImplementedException)
                {
                    context.Result = new FunctionResult(context.Result, "Result ignoring exception.");
                }
            });

        // Act
        var result = await kernel.InvokeAsync(function);
        var resultValue = result.GetValue<string>();

        // Assert
        Assert.Equal("Result ignoring exception.", resultValue);
    }

    [Fact]
    public async Task FunctionFilterCanHandleExceptionOnStreamingAsync()
    {
        // Arrange
        static async IAsyncEnumerable<string> GetData()
        {
            await Task.Delay(0);
            yield return "first chunk";
            throw new KernelException();
        }

        var function = KernelFunctionFactory.CreateFromMethod(GetData);

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                await next(context);

                async static IAsyncEnumerable<string> ProcessData(IAsyncEnumerable<string> enumerable)
                {
                    var enumerator = enumerable.GetAsyncEnumerator();

                    await using (enumerator.ConfigureAwait(false))
                    {
                        while (true)
                        {
                            string result;

                            try
                            {
                                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                                {
                                    break;
                                }

                                result = enumerator.Current;
                            }
                            catch (KernelException)
                            {
                                result = "chunk instead of exception";
                            }

                            yield return result;
                        }
                    }
                }

                var enumerable = context.Result.GetValue<IAsyncEnumerable<string>>();
                context.Result = new FunctionResult(context.Result, ProcessData(enumerable!));
            });

        // Act
        var resultArray = new List<string>();

        await foreach (var item in kernel.InvokeStreamingAsync<string>(function))
        {
            resultArray.Add(item);
        }

        // Assert
        Assert.Equal("first chunk", resultArray[0]);
        Assert.Equal("chunk instead of exception", resultArray[1]);
    }

    [Fact]
    public async Task FunctionFilterCanRethrowNewExceptionAsync()
    {
        // Arrange
        var function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException("Exception from method"); });

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (KernelException)
                {
                    throw new KernelException("Exception from filter");
                }
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Exception from filter", exception.Message);
    }

    [Fact]
    public async Task FunctionFilterCanRethrowNewExceptionOnStreamingAsync()
    {
        // Arrange
        static async IAsyncEnumerable<string> GetData()
        {
            await Task.Delay(0);
            yield return "first chunk";
            throw new KernelException("Exception from method");
        }

        var function = KernelFunctionFactory.CreateFromMethod(GetData);

        var kernel = this.GetKernelWithFilters(
            onFunctionInvocation: async (context, next) =>
            {
                await next(context);

                async static IAsyncEnumerable<string> ProcessData(IAsyncEnumerable<string> enumerable)
                {
                    var enumerator = enumerable.GetAsyncEnumerator();

                    await using (enumerator.ConfigureAwait(false))
                    {
                        while (true)
                        {
                            try
                            {
                                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                                {
                                    break;
                                }
                            }
                            catch (KernelException)
                            {
                                throw new KernelException("Exception from filter");
                            }

                            yield return enumerator.Current;
                        }
                    }
                }

                var enumerable = context.Result.GetValue<IAsyncEnumerable<string>>();
                context.Result = new FunctionResult(context.Result, ProcessData(enumerable!));
            });

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(async () =>
        {
            await foreach (var item in kernel.InvokeStreamingAsync<string>(function))
            { }
        });

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Exception from filter", exception.Message);
    }

    [Fact]
    public async Task MultipleFunctionFiltersReceiveInvocationExceptionAsync()
    {
        // Arrange
        int filterInvocations = 0;
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException(); });

        async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            try
            {
                await next(context);
            }
            catch (KernelException)
            {
                filterInvocations++;
                throw;
            }
        }

        var functionFilter1 = new FakeFunctionFilter(OnFunctionInvocationAsync);
        var functionFilter2 = new FakeFunctionFilter(OnFunctionInvocationAsync);
        var functionFilter3 = new FakeFunctionFilter(OnFunctionInvocationAsync);

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter3);

        var kernel = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(() => kernel.InvokeAsync(function));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(3, filterInvocations);
    }

    [Fact]
    public async Task MultipleFunctionFiltersPropagateExceptionAsync()
    {
        // Arrange
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException("Exception from method"); });

        var functionFilter1 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from functionFilter2", exception.Message);
                context.Result = new FunctionResult(context.Result, "Result from functionFilter1");
            }
        });

        var functionFilter2 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from functionFilter3", exception.Message);
                throw new KernelException("Exception from functionFilter2");
            }
        });

        var functionFilter3 = new FakeFunctionFilter(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (KernelException exception)
            {
                Assert.Equal("Exception from method", exception.Message);
                throw new KernelException("Exception from functionFilter3");
            }
        });

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter3);

        var kernel = builder.Build();

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("Result from functionFilter1", result.ToString());
    }

    [Fact]
    public async Task MultipleFunctionFiltersPropagateExceptionOnStreamingAsync()
    {
        // Arrange
        int filterInvocations = 0;
        KernelFunction function = KernelFunctionFactory.CreateFromMethod(() => { throw new KernelException("Exception from method"); });

        async Task OnFunctionInvocationAsync(
            string expectedExceptionMessage,
            string exceptionMessage,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, Task> next)
        {
            await next(context);

            async IAsyncEnumerable<string> ProcessData(IAsyncEnumerable<string> enumerable)
            {
                var enumerator = enumerable.GetAsyncEnumerator();

                await using (enumerator.ConfigureAwait(false))
                {
                    while (true)
                    {
                        try
                        {
                            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                            {
                                break;
                            }
                        }
                        catch (KernelException exception)
                        {
                            filterInvocations++;
                            Assert.Equal(expectedExceptionMessage, exception.Message);

                            throw new KernelException(exceptionMessage);
                        }

                        yield return enumerator.Current;
                    }
                }
            }

            var enumerable = context.Result.GetValue<IAsyncEnumerable<string>>();
            context.Result = new FunctionResult(context.Result, ProcessData(enumerable!));
        }

        var functionFilter1 = new FakeFunctionFilter(
            async (context, next) => await OnFunctionInvocationAsync("Exception from functionFilter2", "Exception from functionFilter1", context, next));

        var functionFilter2 = new FakeFunctionFilter(
            async (context, next) => await OnFunctionInvocationAsync("Exception from functionFilter3", "Exception from functionFilter2", context, next));

        var functionFilter3 = new FakeFunctionFilter(
            async (context, next) => await OnFunctionInvocationAsync("Exception from method", "Exception from functionFilter3", context, next));

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton<IFunctionFilter>(functionFilter1);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter2);
        builder.Services.AddSingleton<IFunctionFilter>(functionFilter3);

        var kernel = builder.Build();

        // Act
        var exception = await Assert.ThrowsAsync<KernelException>(async () =>
        {
            await foreach (var item in kernel.InvokeStreamingAsync<string>(function))
            { }
        });

        // Assert
        Assert.NotNull(exception);
        Assert.Equal("Exception from functionFilter1", exception.Message);
        Assert.Equal(3, filterInvocations);
    }

    [Fact]
    public async Task FunctionFiltersForMethodCanOverrideResultAsync()
    {
        // Arrange
        const int OriginalResult = 42;
        const int NewResult = 84;

        var function = KernelFunctionFactory.CreateFromMethod(() => OriginalResult);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            await next(context);
            context.Result = new FunctionResult(context.Result, NewResult);
        });

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal(NewResult, result.GetValue<int>());
    }

    [Fact]
    public async Task FunctionFiltersForMethodCanOverrideResultAsyncOnStreamingAsync()
    {
        // Arrange
        static async IAsyncEnumerable<int> GetData()
        {
            await Task.Delay(0);
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var function = KernelFunctionFactory.CreateFromMethod(GetData);

        var kernel = this.GetKernelWithFilters(onFunctionInvocation: async (context, next) =>
        {
            await next(context);

            async static IAsyncEnumerable<int> GetModifiedData(IAsyncEnumerable<int> enumerable)
            {
                await foreach (var item in enumerable)
                {
                    yield return item * 2;
                }
            }

            var enumerable = context.Result.GetValue<IAsyncEnumerable<int>>();
            context.Result = new FunctionResult(context.Result, GetModifiedData(enumerable!));
        });

        // Act
        var resultArray = new List<int>();

        await foreach (var item in kernel.InvokeStreamingAsync<int>(function))
        {
            resultArray.Add(item);
        }

        // Assert
        Assert.Equal(2, resultArray[0]);
        Assert.Equal(4, resultArray[1]);
        Assert.Equal(6, resultArray[2]);
    }

    [Fact]
    public async Task FunctionFiltersForPromptCanOverrideResultAsync()
    {
        // Arrange
        var mockMetadata = new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        var mockTextGeneration = this.GetMockTextGeneration("Result from prompt function", mockMetadata);

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onFunctionInvocation: async (context, next) =>
            {
                await next(context);

                Assert.NotNull(context.Result.Metadata);

                var metadata = new Dictionary<string, object?>(context.Result.Metadata)
                {
                    ["key3"] = "value3"
                };

                metadata["key2"] = "updated_value2";

                context.Result = new FunctionResult(context.Function, "Result from filter")
                {
                    Culture = CultureInfo.CurrentCulture,
                    Metadata = metadata
                };
            });

        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");

        // Act
        var result = await kernel.InvokeAsync(function);

        // Assert
        Assert.Equal("Result from filter", result.GetValue<string>());
        Assert.NotNull(result.Metadata);
        Assert.Equal("value1", result.Metadata["key1"]);
        Assert.Equal("updated_value2", result.Metadata["key2"]);
        Assert.Equal("value3", result.Metadata["key3"]);
        Assert.Equal(CultureInfo.CurrentCulture, result.Culture);

        mockTextGeneration.Verify(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task FunctionFiltersForPromptCanOverrideResultOnStreamingAsync()
    {
        // Arrange
        var mockMetadata = new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        var mockTextGeneration = this.GetMockTextGeneration("result chunk from prompt function", mockMetadata);

        var kernel = this.GetKernelWithFilters(textGenerationService: mockTextGeneration.Object,
            onFunctionInvocation: async (context, next) =>
            {
                await next(context);

                async static IAsyncEnumerable<StreamingTextContent> OverrideResult(IAsyncEnumerable<StreamingTextContent> enumerable)
                {
                    await foreach (var item in enumerable)
                    {
                        Assert.NotNull(item.Metadata);
                        var metadata = new Dictionary<string, object?>(item.Metadata)
                        {
                            ["key3"] = "value3"
                        };

                        metadata["key2"] = "updated_value2";

                        yield return new StreamingTextContent("result chunk from filter", metadata: metadata);
                    }
                }

                var enumerable = context.Result.GetValue<IAsyncEnumerable<StreamingTextContent>>();
                Assert.NotNull(enumerable);

                context.Result = new FunctionResult(context.Result, OverrideResult(enumerable));
            });

        var function = KernelFunctionFactory.CreateFromPrompt("Write a simple phrase about UnitTests");

        // Act
        var result = new List<StreamingTextContent>();
        await foreach (var item in kernel.InvokeStreamingAsync<StreamingTextContent>(function))
        {
            result.Add(item);
        }

        var resultChunk = result[0];

        // Assert
        Assert.NotNull(resultChunk);
        Assert.Equal("result chunk from filter", resultChunk.Text);
        Assert.NotNull(resultChunk.Metadata);
        Assert.Equal("value1", resultChunk.Metadata["key1"]);
        Assert.Equal("updated_value2", resultChunk.Metadata["key2"]);
        Assert.Equal("value3", resultChunk.Metadata["key3"]);

        mockTextGeneration.Verify(m => m.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    private Kernel GetKernelWithFilters(
        Func<FunctionInvocationContext, Func<FunctionInvocationContext, Task>, Task>? onFunctionInvocation = null,
        Action<PromptRenderingContext>? onPromptRendering = null,
        Action<PromptRenderedContext>? onPromptRendered = null,
        ITextGenerationService? textGenerationService = null)
    {
        var builder = Kernel.CreateBuilder();
        var promptFilter = new FakePromptFilter(onPromptRendering, onPromptRendered);

        // Add function filter before kernel construction
        if (onFunctionInvocation is not null)
        {
            var functionFilter = new FakeFunctionFilter(onFunctionInvocation);
            builder.Services.AddSingleton<IFunctionFilter>(functionFilter);
        }

        if (textGenerationService is not null)
        {
            builder.Services.AddSingleton<ITextGenerationService>(textGenerationService);
        }

        var kernel = builder.Build();

        // Add prompt filter after kernel construction
        kernel.PromptFilters.Add(promptFilter);

        return kernel;
    }

    private Mock<ITextGenerationService> GetMockTextGeneration(string? textResult = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var mockTextGeneration = new Mock<ITextGenerationService>();
        mockTextGeneration
            .Setup(m => m.GetTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TextContent> { new(textResult ?? "result text", metadata: metadata) });

        mockTextGeneration
            .Setup(s => s.GetStreamingTextContentsAsync(It.IsAny<string>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .Returns(new List<StreamingTextContent>() { new(textResult ?? "result chunk", metadata: metadata) }.ToAsyncEnumerable());

        return mockTextGeneration;
    }

    private sealed class FakeFunctionFilter(
        Func<FunctionInvocationContext, Func<FunctionInvocationContext, Task>, Task>? onFunctionInvocation) : IFunctionFilter
    {
        private readonly Func<FunctionInvocationContext, Func<FunctionInvocationContext, Task>, Task>? _onFunctionInvocation = onFunctionInvocation;

        public Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next) =>
            this._onFunctionInvocation?.Invoke(context, next) ?? Task.CompletedTask;
    }

    private sealed class FakePromptFilter(
        Action<PromptRenderingContext>? onPromptRendering = null,
        Action<PromptRenderedContext>? onPromptRendered = null) : IPromptFilter
    {
        private readonly Action<PromptRenderingContext>? _onPromptRendering = onPromptRendering;
        private readonly Action<PromptRenderedContext>? _onPromptRendered = onPromptRendered;

        public void OnPromptRendered(PromptRenderedContext context) =>
            this._onPromptRendered?.Invoke(context);

        public void OnPromptRendering(PromptRenderingContext context) =>
            this._onPromptRendering?.Invoke(context);
    }
}
