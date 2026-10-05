// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

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

    [Test]
    public async Task WhoAmI_UserWithoutOrganization_FallsBackToOptionsOrganizationId()
    {
        var sut = new FakeOrganizationService();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        sut.Options.UserId = userId;
        sut.Options.OrganizationId = orgId;
        sut.Add(new Entity("systemuser") { Id = userId });

        var result = (WhoAmIResponse)sut.Execute(new WhoAmIRequest());

        await Assert.That(result.OrganizationId).IsEqualTo(orgId);
    }

    [Test]
    public async Task WhoAmI_UserOrganization_TakesPrecedenceOverOptions()
    {
        var sut = new FakeOrganizationService();
        var userId = Guid.NewGuid();
        var userOrgId = Guid.NewGuid();
        sut.Options.UserId = userId;
        sut.Options.OrganizationId = Guid.NewGuid();
        sut.Add(new Entity("systemuser") { Id = userId, ["organizationid"] = userOrgId });

        var result = (WhoAmIResponse)sut.Execute(new WhoAmIRequest());

        await Assert.That(result.OrganizationId).IsEqualTo(userOrgId);
    }

    [Test]
    public async Task WhoAmI_UnknownUserAndNoOrganizationId_OmitsOrganizationAndBusinessUnit()
    {
        var sut = new FakeOrganizationService();

        var result = (WhoAmIResponse)sut.Execute(new WhoAmIRequest());

        await Assert.That(result.Results.Contains("OrganizationId")).IsFalse();
        await Assert.That(result.Results.Contains("BusinessUnitId")).IsFalse();
    }
}
