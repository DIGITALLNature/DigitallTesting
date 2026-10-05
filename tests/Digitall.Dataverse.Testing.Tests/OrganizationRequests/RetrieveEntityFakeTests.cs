// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveEntityFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        _sut.AddRequest(new RetrieveEntityFake());
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_RegisteredMetadata_ReturnsEntityMetadata()
    {
        var metadata = new EntityMetadata { LogicalName = "account" };
        _sut.AddMetadata(metadata);

        var response = (RetrieveEntityResponse)_sut.Execute(new RetrieveEntityRequest
        {
            LogicalName = "account"
        });

        await Assert.That(response.EntityMetadata).IsNotNull();
        await Assert.That(response.EntityMetadata.LogicalName).IsEqualTo("account");
    }

    [Test]
    public async Task Execute_MetadataWithAttributes_ReturnsAttributes()
    {
        var metadata = new EntityMetadata { LogicalName = "account" };
        var nameAttribute = new StringAttributeMetadata("name") { LogicalName = "name" };

        // Use reflection to set Attributes since it has no public setter
        var attributesProperty = typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.Attributes));
        attributesProperty!.SetValue(metadata, new AttributeMetadata[] { nameAttribute });

        _sut.AddMetadata(metadata);

        var response = (RetrieveEntityResponse)_sut.Execute(new RetrieveEntityRequest
        {
            LogicalName = "account"
        });

        await Assert.That(response.EntityMetadata.Attributes).IsNotNull();
        await Assert.That(response.EntityMetadata.Attributes).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata.Attributes[0].LogicalName).IsEqualTo("name");
    }

    [Test]
    public async Task Execute_NonRegisteredMetadata_ThrowsException()
    {
        _sut.AddMetadata(new EntityMetadata { LogicalName = "account" });

        void Action() => _sut.Execute(new RetrieveEntityRequest { LogicalName = "contact" });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.QueryBuilderNoEntity);
    }

    [Test]
    public async Task Execute_RegisteredMetadataId_ReturnsEntityMetadata()
    {
        var metaId = Guid.NewGuid();
        var metadata = new EntityMetadata { LogicalName = "account", MetadataId = metaId };
        _sut.AddMetadata(metadata);

        var response = (RetrieveEntityResponse)_sut.Execute(new RetrieveEntityRequest
        {
            MetadataId = metaId
        });

        await Assert.That(response.EntityMetadata).IsNotNull();
        await Assert.That(response.EntityMetadata.LogicalName).IsEqualTo("account");
    }

    [Test]
    public async Task Execute_NonRegisteredMetadataId_ThrowsFault()
    {
        var nonExistentId = Guid.NewGuid();

        void Action() => _sut.Execute(new RetrieveEntityRequest { MetadataId = nonExistentId });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.QueryBuilderNoEntity);
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNull()
    {
        void Action() => _sut.Execute(null!);

        Assert.Throws<ArgumentNullException>(Action);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_ReturnsCorrectResponseType()
    {
        _sut.AddMetadata(new EntityMetadata { LogicalName = "account" });

        var response = _sut.Execute(new RetrieveEntityRequest { LogicalName = "account" });

        await Assert.That(response).IsTypeOf<RetrieveEntityResponse>();
    }
}
