// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Extensions;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveCurrentOrganizationFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_DefaultOptions_ReturnsDefaults()
    {
        var request = new RetrieveCurrentOrganizationRequest();
        var response = (RetrieveCurrentOrganizationResponse)_sut.Execute(request);

        await Assert.That(response.Detail).IsNotNull();
        await Assert.That(response.Detail.OrganizationId).IsEqualTo(Guid.Empty);
        await Assert.That(response.Detail.UniqueName).IsEqualTo("org");
        await Assert.That(response.Detail.FriendlyName).IsEqualTo("Fake Organization");
        await Assert.That(response.Detail.OrganizationVersion).IsEqualTo("9.2.0.0");
    }

    [Test]
    public async Task Execute_CustomOptions_ReturnsCustomValues()
    {
        var orgId = Guid.NewGuid();
        _sut.Options.OrganizationId = orgId;
        _sut.Options.OrganizationUniqueName = "myorg";
        _sut.Options.OrganizationFriendlyName = "My Org Display";
        _sut.Options.OrganizationVersion = "9.2.24051.00170";

        var request = new RetrieveCurrentOrganizationRequest();
        var response = (RetrieveCurrentOrganizationResponse)_sut.Execute(request);

        await Assert.That(response.Detail).IsNotNull();
        await Assert.That(response.Detail.OrganizationId).IsEqualTo(orgId);
        await Assert.That(response.Detail.UniqueName).IsEqualTo("myorg");
        await Assert.That(response.Detail.FriendlyName).IsEqualTo("My Org Display");
        await Assert.That(response.Detail.OrganizationVersion).IsEqualTo("9.2.24051.00170");
    }

    [Test]
    public async Task Execute_WithOrganizationEntityInState_UsesEntityValues()
    {
        var orgId = Guid.NewGuid();
        var orgEntity = new Entity("organization")
        {
            Id = orgId,
            ["uniquename"] = "crmprod",
            ["name"] = "CRM Production"
        };
        _sut.Add(orgEntity);

        var request = new RetrieveCurrentOrganizationRequest();
        var response = (RetrieveCurrentOrganizationResponse)_sut.Execute(request);

        await Assert.That(response.Detail).IsNotNull();
        await Assert.That(response.Detail.OrganizationId).IsEqualTo(orgId);
        await Assert.That(response.Detail.UniqueName).IsEqualTo("crmprod");
        await Assert.That(response.Detail.FriendlyName).IsEqualTo("CRM Production");
    }

    [Test]
    public async Task Execute_EntityWithoutName_FallsBackToOptions()
    {
        var orgId = Guid.NewGuid();
        _sut.Options.OrganizationId = Guid.NewGuid();
        _sut.Options.OrganizationFriendlyName = "Option Name";
        _sut.Add(new Entity("organization") { Id = orgId, ["uniquename"] = "crmprod" });

        var response = (RetrieveCurrentOrganizationResponse)_sut.Execute(new RetrieveCurrentOrganizationRequest());

        await Assert.That(response.Detail.OrganizationId).IsEqualTo(orgId);
        await Assert.That(response.Detail.UniqueName).IsEqualTo("crmprod");
        await Assert.That(response.Detail.FriendlyName).IsEqualTo("Option Name");
    }

    [Test]
    public async Task Builder_WithOrganization_SetsOptions()
    {
        var orgId = Guid.NewGuid();
        var service = new FakeDataverseBuilder()
            .WithOrganizationId(orgId)
            .WithOrganizationName("customorg", "Custom Friendly Name")
            .GetOrganizationService();

        var response = (RetrieveCurrentOrganizationResponse)service.Execute(new RetrieveCurrentOrganizationRequest());

        await Assert.That(response.Detail).IsNotNull();
        await Assert.That(response.Detail.OrganizationId).IsEqualTo(orgId);
        await Assert.That(response.Detail.UniqueName).IsEqualTo("customorg");
        await Assert.That(response.Detail.FriendlyName).IsEqualTo("Custom Friendly Name");
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNullException()
    {
        var fake = new RetrieveCurrentOrganizationFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
