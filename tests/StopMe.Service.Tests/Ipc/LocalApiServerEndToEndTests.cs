using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StopMe.LocalApi;
using StopMe.LocalApi.Client;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

public class LocalApiServerEndToEndTests
{
    [Fact]
    public async Task Client_can_call_status_through_real_named_pipe()
    {
        var pipeName = "StopMe.TestPipe." + Guid.NewGuid().ToString("N");
        var options = new ServiceOptions
        {
            MaxConcurrentPipeConnections = 1,
            PipeNameOverride = pipeName,
        };
        await using var host = new TestHost(options: options);

        using var cts = new CancellationTokenSource();
        var server = host.Services.GetRequiredService<LocalApiServer>();
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        try
        {
            await using var client = new StopMeLocalApiClient(token: host.Token, pipeNameOverride: pipeName);
            await client.ConnectAsync(TimeSpan.FromSeconds(5));

            var response = await client.StatusAsync();

            response.Should().NotBeNull();
            response!.Success.Should().BeTrue();
            response.PayloadAs<StatusDto>().Should().NotBeNull();
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public async Task Client_with_invalid_token_is_rejected()
    {
        var pipeName = "StopMe.TestPipe." + Guid.NewGuid().ToString("N");
        var options = new ServiceOptions
        {
            MaxConcurrentPipeConnections = 1,
            PipeNameOverride = pipeName,
        };
        await using var host = new TestHost(options: options);

        using var cts = new CancellationTokenSource();
        var server = host.Services.GetRequiredService<LocalApiServer>();
        var serverTask = Task.Run(() => server.RunAsync(cts.Token), cts.Token);

        try
        {
            await using var client = new StopMeLocalApiClient(token: "WRONG", pipeNameOverride: pipeName);
            await client.ConnectAsync(TimeSpan.FromSeconds(5));

            var response = await client.StatusAsync();
            response.Should().NotBeNull();
            response!.Success.Should().BeFalse();
            response.ErrorCode.Should().Be(ErrorCodes.Unauthorized);
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
