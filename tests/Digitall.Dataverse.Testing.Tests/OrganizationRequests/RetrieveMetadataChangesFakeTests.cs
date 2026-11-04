// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveMetadataChangesFakeTests
{
    [Test]
    public async Task Execute_Should_Return_Entity_And_Key_Projection()
    {
        var metadataId = Guid.NewGuid();
        var keyMetadataId = Guid.NewGuid();
        var entityMetadata = new EntityMetadata
        {
            LogicalName = "account",
            MetadataId = metadataId
        };
        SetEntityKeys(
            entityMetadata,
            [
                new EntityKeyMetadata
                {
                    LogicalName = "ak_account_name",
                    MetadataId = keyMetadataId,
                    SchemaName = "AK_account_name"
                }
            ]);

        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var request = new RetrieveMetadataChangesRequest
        {
            Query = new EntityQueryExpression
            {
                Criteria = new MetadataFilterExpression
                {
                    Conditions =
                    {
                        new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, metadataId)
                    }
                },
                Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.Keys)),
                KeyQuery = new EntityKeyQueryExpression
                {
                    Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityKeyMetadata.LogicalName))
                }
            }
        };

        var response = (RetrieveMetadataChangesResponse)service.Execute(request);

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].MetadataId).IsEqualTo(metadataId);
        await Assert.That(response.EntityMetadata[0].LogicalName).IsNull();
        await Assert.That(response.EntityMetadata[0].Keys).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].Keys[0].MetadataId).IsEqualTo(keyMetadataId);
        await Assert.That(response.EntityMetadata[0].Keys[0].LogicalName).IsEqualTo("ak_account_name");
        await Assert.That(response.EntityMetadata[0].Keys[0].SchemaName).IsNull();
        await Assert.That(entityMetadata.Keys[0].SchemaName).IsEqualTo("AK_account_name");
    }

    [Test]
    public async Task Execute_Should_Return_Empty_Results_When_No_Metadata_Matches()
    {
        var service = new FakeOrganizationService();
        service.AddMetadata(new EntityMetadata { LogicalName = "account", MetadataId = Guid.NewGuid() });
        var request = CreateRequest(Guid.NewGuid());

        var response = (RetrieveMetadataChangesResponse)service.Execute(request);

        await Assert.That(response.EntityMetadata).IsEmpty();
    }

    [Test]
    public async Task Execute_Should_Distinguish_Entity_Without_Keys_From_Missing_Metadata()
    {
        var metadataId = Guid.NewGuid();
        var service = new FakeOrganizationService();
        service.AddMetadata(new EntityMetadata { LogicalName = "account", MetadataId = metadataId });

        var response = (RetrieveMetadataChangesResponse)service.Execute(CreateRequest(metadataId));

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].Keys).IsEmpty();
    }

    [Test]
    public async Task Execute_Should_Reject_Client_Version_Stamp()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();
        var request = CreateRequest();
        request.ClientVersionStamp = "previous-stamp";

        await Assert.That(() => service.Execute(request)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Execute_Should_Reject_Unsupported_Entity_Properties()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();
        var request = CreateRequest();
        request.Query.Properties = new MetadataPropertiesExpression(nameof(EntityMetadata.LogicalName));

        await Assert.That(() => service.Execute(request)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Execute_Should_Reject_Unsupported_Metadata_Criteria()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();
        var request = CreateRequest();
        request.Query.Criteria.Conditions.Add(
            new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.NotEquals, Guid.NewGuid()));

        await Assert.That(() => service.Execute(request)).Throws<NotSupportedException>();
    }

    private static RetrieveMetadataChangesRequest CreateRequest(Guid? metadataId = null)
    {
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.Keys)),
            KeyQuery = new EntityKeyQueryExpression
            {
                Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityKeyMetadata.LogicalName))
            }
        };
        if (metadataId.HasValue)
        {
            query.Criteria.Conditions.Add(
                new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, metadataId.Value));
        }

        return new RetrieveMetadataChangesRequest { Query = query };
    }

    private static void SetEntityKeys(EntityMetadata entityMetadata, EntityKeyMetadata[] keys)
    {
        var keysProperty = typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.Keys))
                           ?? throw new InvalidOperationException("The SDK EntityMetadata.Keys property is unavailable.");
        keysProperty.SetValue(entityMetadata, keys);
    }
}
