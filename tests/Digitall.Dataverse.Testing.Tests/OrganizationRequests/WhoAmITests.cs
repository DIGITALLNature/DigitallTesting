// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class WhoAmITests
{
    [Test]
    public async Task Stubs_Dispatch_Working()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new WhoAmIFake());

        var result = sut.Execute(new WhoAmIRequest());

        await Assert.That(result).IsTypeOf<WhoAmIResponse>();
    }

    [Test]
    public async Task WhoAmI_WithOptionsOrganizationId_ReturnsOrganizationId()
    {
        var sut = new FakeOrganizationService();
        var orgId = Guid.NewGuid();
        sut.Options.OrganizationId = orgId;

        var result = (WhoAmIResponse)sut.Execute(new WhoAmIRequest());

        await Assert.That(result.OrganizationId).IsEqualTo(orgId);
    }
}
