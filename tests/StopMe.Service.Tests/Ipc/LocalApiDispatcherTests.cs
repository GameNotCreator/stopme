using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

public class LocalApiDispatcherTests
{
    [Fact]
    public async Task Rejects_request_without_token()
    {
        await using var host = new TestHost();
        var dispatcher = host.Services.GetRequiredService<LocalApiDispatcher>();
        var resp = await dispatcher.DispatchAsync(new LocalApiRequest { Op = Operations.Status }, default);

        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task Rejects_unknown_operation()
    {
        await using var host = new TestHost();
        var dispatcher = host.Services.GetRequiredService<LocalApiDispatcher>();
        var resp = await dispatcher.DispatchAsync(new LocalApiRequest { Op = "garbage", Token = host.Token }, default);

        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.UnknownOperation);
    }

    [Fact]
    public async Task Routes_to_status_handler_when_authorized()
    {
        await using var host = new TestHost();
        var dispatcher = host.Services.GetRequiredService<LocalApiDispatcher>();
        var resp = await dispatcher.DispatchAsync(new LocalApiRequest { Op = Operations.Status, Token = host.Token }, default);

        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<StatusDto>();
        dto.Should().NotBeNull();
        dto!.ServiceVersion.Should().NotBeNullOrEmpty();
    }
}
