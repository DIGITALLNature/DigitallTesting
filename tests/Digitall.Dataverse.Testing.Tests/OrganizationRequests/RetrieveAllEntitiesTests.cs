// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveAllEntitiesTests
{
    [Test]
    public async Task Execute_Should_Return_Empty_When_No_Metadata_Registered()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var response = (RetrieveAllEntitiesResponse)sut.Execute(new RetrieveAllEntitiesRequest());

        await Assert.That(response).IsNotNull();
        await Assert.That(response.EntityMetadata).IsNotNull();
        await Assert.That(response.EntityMetadata).IsEmpty();
    }

    [Test]
    public async Task Execute_Should_Return_All_Registered_EntityMetadata()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var accountMetadata = new EntityMetadata { LogicalName = "account" };
        var contactMetadata = new EntityMetadata { LogicalName = "contact" };
        sut.State.EntityMetadata["account"] = accountMetadata;
        sut.State.EntityMetadata["contact"] = contactMetadata;

        var response = (RetrieveAllEntitiesResponse)sut.Execute(new RetrieveAllEntitiesRequest());

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(2);
        await Assert.That(response.EntityMetadata.Select(m => m.LogicalName)).Contains("account");
        await Assert.That(response.EntityMetadata.Select(m => m.LogicalName)).Contains("contact");
    }

    [Test]
    public async Task Execute_Should_Return_Metadata_Projection_Without_Changing_Registered_Metadata()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var metadata = new EntityMetadata { LogicalName = "account" };
        SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), new AttributeMetadata[]
        {
            new StringAttributeMetadata { LogicalName = "name" }
        });
        sut.State.EntityMetadata["account"] = metadata;

        var response = (RetrieveAllEntitiesResponse)sut.Execute(new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Entity });

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0]).IsNotSameReferenceAs(metadata);
        await Assert.That(response.EntityMetadata[0].Attributes).IsNull();
        await Assert.That(metadata.Attributes).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_Should_Return_Attributes_Only_When_Requested()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var metadata = new EntityMetadata { LogicalName = "account" };
        SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), new AttributeMetadata[]
        {
            new StringAttributeMetadata { LogicalName = "name" }
        });
        SetMetadataProperty(metadata, nameof(EntityMetadata.OneToManyRelationships), new OneToManyRelationshipMetadata[]
        {
            new() { SchemaName = "account_contacts" }
        });
        sut.State.EntityMetadata["account"] = metadata;

        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Attributes
        };

        var response = (RetrieveAllEntitiesResponse)sut.Execute(request);

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].Attributes).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships).IsNull();
        await Assert.That(metadata.Attributes).Count().IsEqualTo(1);
        await Assert.That(metadata.OneToManyRelationships).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_Should_Return_Relationships_Only_When_Requested()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var metadata = new EntityMetadata { LogicalName = "account" };
        SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), new AttributeMetadata[]
        {
            new StringAttributeMetadata { LogicalName = "name" }
        });
        SetMetadataProperty(metadata, nameof(EntityMetadata.OneToManyRelationships), new OneToManyRelationshipMetadata[]
        {
            new() { SchemaName = "account_contacts" }
        });
        sut.State.EntityMetadata["account"] = metadata;

        var response = (RetrieveAllEntitiesResponse)sut.Execute(
            new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Relationships });

        await Assert.That(response.EntityMetadata[0].Attributes).IsNull();
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships[0]).IsNotSameReferenceAs(metadata.OneToManyRelationships[0]);
    }

    [Test]
    public async Task Execute_Should_Return_Attributes_And_Relationships_When_Both_Are_Requested()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());

        var metadata = new EntityMetadata { LogicalName = "account" };
        SetMetadataProperty(metadata, nameof(EntityMetadata.Attributes), new AttributeMetadata[]
        {
            new StringAttributeMetadata { LogicalName = "name" }
        });
        SetMetadataProperty(metadata, nameof(EntityMetadata.OneToManyRelationships), new OneToManyRelationshipMetadata[]
        {
            new() { SchemaName = "account_contacts" }
        });
        sut.State.EntityMetadata["account"] = metadata;

        var response = (RetrieveAllEntitiesResponse)sut.Execute(
            new RetrieveAllEntitiesRequest { EntityFilters = EntityFilters.Attributes | EntityFilters.Relationships });

        await Assert.That(response.EntityMetadata[0].Attributes).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_Should_Ignore_RetrieveAsIfPublished_When_Using_Single_Metadata_State()
    {
        var sut = new FakeOrganizationService();
        sut.AddRequest(new RetrieveAllEntitiesFake());
        var metadata = new EntityMetadata { LogicalName = "account" };
        sut.State.EntityMetadata["account"] = metadata;

        var publishedResponse = (RetrieveAllEntitiesResponse)sut.Execute(new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = true
        });
        publishedResponse.EntityMetadata[0].LogicalName = "changed";
        var unpublishedResponse = (RetrieveAllEntitiesResponse)sut.Execute(new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        });

        await Assert.That(publishedResponse.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(unpublishedResponse.EntityMetadata[0].LogicalName).IsEqualTo("account");
        await Assert.That(metadata.LogicalName).IsEqualTo("account");
    }

    private static void SetMetadataProperty(EntityMetadata metadata, string propertyName, object? value)
    {
        var property = typeof(EntityMetadata).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"The SDK EntityMetadata.{propertyName} property is unavailable.");
        property.SetValue(metadata, value);
    }
}
