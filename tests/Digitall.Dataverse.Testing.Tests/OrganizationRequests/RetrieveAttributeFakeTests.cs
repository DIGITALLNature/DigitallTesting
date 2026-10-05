// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveAttributeFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        _sut.AddRequest(new RetrieveAttributeFake());
        await Task.CompletedTask;
    }

    private static EntityMetadata CreateMetadataWithAttribute(string entityLogicalName, AttributeMetadata attribute)
    {
        var metadata = new EntityMetadata { LogicalName = entityLogicalName };

        // Use reflection to set Attributes since it has no public setter
        var attributesProperty = typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.Attributes));
        attributesProperty!.SetValue(metadata, new[] { attribute });

        return metadata;
    }

    [Test]
    public async Task Execute_RegisteredAttribute_ReturnsAttributeMetadata()
    {
        var nameAttribute = new StringAttributeMetadata("name") { LogicalName = "name" };
        _sut.AddMetadata(CreateMetadataWithAttribute("account", nameAttribute));

        var response = (RetrieveAttributeResponse)_sut.Execute(new RetrieveAttributeRequest
        {
            EntityLogicalName = "account",
            LogicalName = "name"
        });

        await Assert.That(response.AttributeMetadata).IsNotNull();
        await Assert.That(response.AttributeMetadata.LogicalName).IsEqualTo("name");
    }

    [Test]
    public async Task Execute_NonRegisteredEntity_ThrowsException()
    {
        var nameAttribute = new StringAttributeMetadata("name") { LogicalName = "name" };
        _sut.AddMetadata(CreateMetadataWithAttribute("account", nameAttribute));

        void Action() => _sut.Execute(new RetrieveAttributeRequest
        {
            EntityLogicalName = "contact",
            LogicalName = "name"
        });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.QueryBuilderNoEntity);
    }

    [Test]
    public async Task Execute_NonRegisteredAttribute_ThrowsException()
    {
        var nameAttribute = new StringAttributeMetadata("name") { LogicalName = "name" };
        _sut.AddMetadata(CreateMetadataWithAttribute("account", nameAttribute));

        void Action() => _sut.Execute(new RetrieveAttributeRequest
        {
            EntityLogicalName = "account",
            LogicalName = "missing"
        });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.QueryBuilderNoAttribute);
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
        var nameAttribute = new StringAttributeMetadata("name") { LogicalName = "name" };
        _sut.AddMetadata(CreateMetadataWithAttribute("account", nameAttribute));

        var response = _sut.Execute(new RetrieveAttributeRequest
        {
            EntityLogicalName = "account",
            LogicalName = "name"
        });

        await Assert.That(response).IsTypeOf<RetrieveAttributeResponse>();
    }
}
