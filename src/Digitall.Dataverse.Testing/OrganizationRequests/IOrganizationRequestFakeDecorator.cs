// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

/// <summary>
/// Decorates a request fake while retaining the ability to invoke the previously registered handler.
/// </summary>
public interface IOrganizationRequestFakeDecorator : IOrganizationRequestFake
{
    /// <summary>
    /// Executes the decoration and optionally delegates to the next registered handler.
    /// </summary>
    OrganizationResponse Execute(
        OrganizationRequest organizationRequest,
        FakeOrganizationService fakeOrganizationService,
        Func<OrganizationResponse> next);
}
