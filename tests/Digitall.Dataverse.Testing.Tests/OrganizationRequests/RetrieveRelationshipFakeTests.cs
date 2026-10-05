// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveRelationshipFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_RetrieveByName_ReturnsRelationshipMetadata()
    {
        var rel = new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts",
            ReferencingEntity = "contact",
            ReferencedEntity = "account"
        };
        _sut.AddRelationship(rel);

        var request = new RetrieveRelationshipRequest { Name = "account_contacts" };
        var response = (RetrieveRelationshipResponse)_sut.Execute(request);

        await Assert.That(response.RelationshipMetadata).IsNotNull();
        await Assert.That(response.RelationshipMetadata.SchemaName).IsEqualTo("account_contacts");
    }

    [Test]
    public async Task Execute_RetrieveByMetadataId_ReturnsRelationshipMetadata()
    {
        var metadataId = Guid.NewGuid();
        var rel = new OneToManyRelationshipMetadata
        {
            SchemaName = "account_tasks",
            MetadataId = metadataId
        };
        _sut.AddRelationship(rel);

        var request = new RetrieveRelationshipRequest { MetadataId = metadataId };
        var response = (RetrieveRelationshipResponse)_sut.Execute(request);

        await Assert.That(response.RelationshipMetadata).IsNotNull();
        await Assert.That(response.RelationshipMetadata.SchemaName).IsEqualTo("account_tasks");
    }

    [Test]
    public async Task Execute_NotFound_ThrowsFaultException()
    {
        var request = new RetrieveRelationshipRequest { Name = "non_existent_rel" };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNullException()
    {
        var fake = new RetrieveRelationshipFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
