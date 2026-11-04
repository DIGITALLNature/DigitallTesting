// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing;

/// <summary>
/// A trackable concrete implementation of OrganizationRequestFake that records all Execute calls
/// and allows configuring the return value via a factory function.
/// </summary>
public sealed class SpyOrganizationRequestFake<TReq, TRes>(Func<TReq, FakeOrganizationService, TRes>? handler = null)
    : OrganizationRequestFake<TReq, TRes>, IOrganizationRequestFakeDecorator
    where TReq : OrganizationRequest
    where TRes : OrganizationResponse, new()
{
    /// <summary>All requests captured by Execute calls.</summary>
    public List<TReq> ReceivedRequests { get; } = [];

    public override TRes Execute(TReq request, FakeOrganizationService fakeOrganizationService)
    {
        ReceivedRequests.Add(request);
        return handler?.Invoke(request, fakeOrganizationService) ?? new TRes();
    }

    OrganizationResponse IOrganizationRequestFakeDecorator.Execute(
        OrganizationRequest organizationRequest,
        FakeOrganizationService fakeOrganizationService,
        Func<OrganizationResponse> next)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);
        ArgumentNullException.ThrowIfNull(next);
        var request = organizationRequest as TReq
                      ?? throw new InvalidCastException($"Cannot cast {organizationRequest.GetType()} to {typeof(TReq)}.");
        ReceivedRequests.Add(request);
        var response = (TRes)next();
        return handler?.Invoke(request, fakeOrganizationService) ?? response;
    }
}
