// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

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
        request.Query.Properties = new MetadataPropertiesExpression(nameof(EntityMetadata.Attributes));

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

    [Test]
    public async Task Execute_Should_Reject_Invalid_In_Criteria_Values()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();
        var request = CreateRequest();
        request.Query.Criteria.Conditions.Add(
            new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.In, new object[] { "not-a-guid" }));

        await Assert.That(() => service.Execute(request)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Execute_Should_Project_Entity_Properties_And_Apply_In_And_Nested_Filters()
    {
        var matchingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var excludedId = Guid.NewGuid();
        var service = new FakeOrganizationService();
        service.AddMetadata(new EntityMetadata { LogicalName = "account", MetadataId = matchingId });
        service.AddMetadata(new EntityMetadata { LogicalName = "contact", MetadataId = otherId });
        service.AddMetadata(new EntityMetadata { LogicalName = "lead", MetadataId = excludedId });

        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(
                nameof(MetadataBase.MetadataId),
                nameof(EntityMetadata.LogicalName),
                nameof(EntityMetadata.SchemaName),
                nameof(EntityMetadata.DisplayName),
                nameof(EntityMetadata.IsManaged),
                nameof(EntityMetadata.IsCustomEntity),
                nameof(EntityMetadata.IsAuditEnabled),
                nameof(EntityMetadata.OwnershipType)),
            Criteria = new MetadataFilterExpression
            {
                Conditions =
                {
                    new MetadataConditionExpression(
                        nameof(MetadataBase.MetadataId),
                        MetadataConditionOperator.In,
                        new object[] { matchingId, otherId, excludedId })
                }
            }
        };
        query.Criteria.Filters.Add(new MetadataFilterExpression(LogicalOperator.Or)
        {
            Conditions =
            {
                new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, matchingId),
                new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, otherId)
            }
        });

        var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });

        await Assert.That(response.EntityMetadata.Select(metadata => metadata.LogicalName)).Contains("account");
        await Assert.That(response.EntityMetadata.Select(metadata => metadata.LogicalName)).Contains("contact");
        await Assert.That(response.EntityMetadata.Select(metadata => metadata.LogicalName)).DoesNotContain("lead");
        await Assert.That(response.EntityMetadata[0].SchemaName).IsNull();
    }

    [Test]
    public async Task Execute_Should_Apply_Key_Criteria_With_Minimal_Entity_Projection()
    {
        var entityId = Guid.NewGuid();
        var presentKeyId = Guid.NewGuid();
        var absentKeyId = Guid.NewGuid();
        var entityMetadata = new EntityMetadata { LogicalName = "account", MetadataId = entityId };
        SetEntityKeys(
            entityMetadata,
            [
                new EntityKeyMetadata
                {
                    LogicalName = "ak_account_name",
                    MetadataId = presentKeyId
                }
            ]);
        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.LogicalName)),
            Criteria = new MetadataFilterExpression
            {
                Conditions =
                {
                    new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, entityId)
                }
            },
            KeyQuery = new EntityKeyQueryExpression
            {
                Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityKeyMetadata.LogicalName)),
                Criteria = new MetadataFilterExpression
                {
                    Conditions =
                    {
                        new MetadataConditionExpression(nameof(MetadataBase.MetadataId), MetadataConditionOperator.Equals, absentKeyId)
                    }
                }
            }
        };

        var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });

        await Assert.That(response.EntityMetadata).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].LogicalName).IsEqualTo("account");
        await Assert.That(response.EntityMetadata[0].Keys).IsEmpty();
        await Assert.That(entityMetadata.Keys[0].MetadataId).IsEqualTo(presentKeyId);
    }

    [Test]
    public async Task Execute_Should_Filter_And_Project_Attribute_Metadata()
    {
        var matchingId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var matchingAttribute = new StringAttributeMetadata
        {
            LogicalName = "name",
            SchemaName = "Name",
            MetadataId = matchingId,
            DisplayName = new Microsoft.Xrm.Sdk.Label("Name label", 1033)
        };
        var entityMetadata = new EntityMetadata { LogicalName = "account", MetadataId = Guid.NewGuid() };
        SetEntityAttributes(entityMetadata, [matchingAttribute, new StringAttributeMetadata
        {
            LogicalName = "description",
            MetadataId = otherId
        }]);
        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.LogicalName)),
            AttributeQuery = new AttributeQueryExpression
            {
                Properties = new MetadataPropertiesExpression(
                    nameof(MetadataBase.MetadataId),
                    nameof(AttributeMetadata.LogicalName),
                    nameof(AttributeMetadata.SchemaName),
                    nameof(AttributeMetadata.DisplayName),
                    nameof(AttributeMetadata.AttributeType),
                    nameof(AttributeMetadata.IsManaged)),
                Criteria = new MetadataFilterExpression
                {
                    Conditions =
                    {
                        new MetadataConditionExpression(
                            nameof(MetadataBase.MetadataId),
                            MetadataConditionOperator.In,
                            new Guid[] { matchingId })
                    }
                }
            }
        };

        var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });
        var projectedAttribute = response.EntityMetadata[0].Attributes[0];

        await Assert.That(response.EntityMetadata[0].Attributes).Count().IsEqualTo(1);
        await Assert.That(projectedAttribute.LogicalName).IsEqualTo("name");
        await Assert.That(projectedAttribute.SchemaName).IsEqualTo("Name");
        await Assert.That(projectedAttribute).IsNotSameReferenceAs(matchingAttribute);
        await Assert.That(projectedAttribute.DisplayName).IsNotSameReferenceAs(matchingAttribute.DisplayName);
    }

    [Test]
    public async Task Execute_Should_Filter_And_Project_All_Relationship_Types()
    {
        var oneToManyId = Guid.NewGuid();
        var manyToManyId = Guid.NewGuid();
        var manyToOneId = Guid.NewGuid();
        var excludedId = Guid.NewGuid();
        var entityMetadata = new EntityMetadata { LogicalName = "account", MetadataId = Guid.NewGuid() };
        SetMetadataProperty(entityMetadata, nameof(EntityMetadata.OneToManyRelationships), new[]
        {
            new OneToManyRelationshipMetadata { MetadataId = oneToManyId, SchemaName = "account_contacts" },
            new OneToManyRelationshipMetadata { MetadataId = excludedId, SchemaName = "account_leads" }
        });
        SetMetadataProperty(entityMetadata, nameof(EntityMetadata.ManyToManyRelationships), new[]
        {
            new ManyToManyRelationshipMetadata { MetadataId = manyToManyId, SchemaName = "account_teams" }
        });
        SetMetadataProperty(entityMetadata, nameof(EntityMetadata.ManyToOneRelationships), new[]
        {
            new OneToManyRelationshipMetadata { MetadataId = manyToOneId, SchemaName = "contact_customer_accounts" }
        });
        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.LogicalName)),
            RelationshipQuery = new RelationshipQueryExpression
            {
                Properties = new MetadataPropertiesExpression(
                    nameof(MetadataBase.MetadataId),
                    nameof(RelationshipMetadataBase.SchemaName),
                    nameof(RelationshipMetadataBase.RelationshipType),
                    nameof(RelationshipMetadataBase.IsManaged)),
                Criteria = new MetadataFilterExpression
                {
                    Conditions =
                    {
                        new MetadataConditionExpression(
                            nameof(MetadataBase.MetadataId),
                            MetadataConditionOperator.In,
                            new object[] { oneToManyId, manyToManyId, manyToOneId })
                    }
                }
            }
        };

        var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });

        await Assert.That(response.EntityMetadata[0].OneToManyRelationships).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships[0].SchemaName).IsEqualTo("account_contacts");
        await Assert.That(response.EntityMetadata[0].ManyToManyRelationships).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].ManyToManyRelationships[0].SchemaName).IsEqualTo("account_teams");
        await Assert.That(response.EntityMetadata[0].ManyToOneRelationships).Count().IsEqualTo(1);
        await Assert.That(response.EntityMetadata[0].ManyToOneRelationships[0].SchemaName).IsEqualTo("contact_customer_accounts");
        await Assert.That(response.EntityMetadata[0].OneToManyRelationships[0])
            .IsNotSameReferenceAs(entityMetadata.OneToManyRelationships[0]);
        await Assert.That(response.EntityMetadata[0].ManyToOneRelationships[0])
            .IsNotSameReferenceAs(entityMetadata.ManyToOneRelationships[0]);
    }

    [Test]
    public async Task Execute_Should_Project_Expanded_Key_Properties()
    {
        var key = new EntityKeyMetadata
        {
            MetadataId = Guid.NewGuid(),
            LogicalName = "ak_account_name",
            SchemaName = "AK_account_name",
            KeyAttributes = ["name"]
        };
        var entityMetadata = new EntityMetadata { LogicalName = "account", MetadataId = Guid.NewGuid() };
        SetEntityKeys(entityMetadata, [key]);
        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.LogicalName)),
            KeyQuery = new EntityKeyQueryExpression
            {
                Properties = new MetadataPropertiesExpression(
                    nameof(MetadataBase.MetadataId),
                    nameof(EntityKeyMetadata.LogicalName),
                    nameof(EntityKeyMetadata.SchemaName),
                    nameof(EntityKeyMetadata.KeyAttributes),
                    nameof(EntityKeyMetadata.EntityKeyIndexStatus),
                    nameof(EntityKeyMetadata.IsManaged))
            }
        };

        var response = (RetrieveMetadataChangesResponse)service.Execute(new RetrieveMetadataChangesRequest { Query = query });
        var projectedKey = response.EntityMetadata[0].Keys[0];

        await Assert.That(projectedKey.LogicalName).IsEqualTo("ak_account_name");
        await Assert.That(projectedKey.SchemaName).IsEqualTo("AK_account_name");
        await Assert.That(projectedKey.KeyAttributes).IsEquivalentTo(["name"]);
        await Assert.That(projectedKey).IsNotSameReferenceAs(key);
        await Assert.That(projectedKey.KeyAttributes).IsNotSameReferenceAs(key.KeyAttributes);
    }

    [Test]
    public async Task Execute_Should_Isolate_Child_Metadata_Between_Queries()
    {
        var key = new EntityKeyMetadata
        {
            MetadataId = Guid.NewGuid(),
            LogicalName = "ak_account_name",
            KeyAttributes = ["name"]
        };
        var entityMetadata = new EntityMetadata { LogicalName = "account", MetadataId = Guid.NewGuid() };
        SetEntityKeys(entityMetadata, [key]);
        var service = new FakeOrganizationService();
        service.AddMetadata(entityMetadata);
        var query = new EntityQueryExpression
        {
            Properties = new MetadataPropertiesExpression(nameof(MetadataBase.MetadataId), nameof(EntityMetadata.LogicalName)),
            KeyQuery = new EntityKeyQueryExpression
            {
                Properties = new MetadataPropertiesExpression(
                    nameof(MetadataBase.MetadataId),
                    nameof(EntityKeyMetadata.LogicalName),
                    nameof(EntityKeyMetadata.KeyAttributes))
            }
        };
        var request = new RetrieveMetadataChangesRequest { Query = query };

        var firstResponse = (RetrieveMetadataChangesResponse)service.Execute(request);
        firstResponse.EntityMetadata[0].Keys[0].KeyAttributes[0] = "changed";
        var secondResponse = (RetrieveMetadataChangesResponse)service.Execute(request);

        await Assert.That(secondResponse.EntityMetadata[0].Keys[0].KeyAttributes[0]).IsEqualTo("name");
        await Assert.That(key.KeyAttributes[0]).IsEqualTo("name");
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

    private static void SetEntityAttributes(EntityMetadata entityMetadata, AttributeMetadata[] attributes)
    {
        SetMetadataProperty(entityMetadata, nameof(EntityMetadata.Attributes), attributes);
    }

    private static void SetMetadataProperty(EntityMetadata entityMetadata, string propertyName, object value)
    {
        var property = typeof(EntityMetadata).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"The SDK EntityMetadata.{propertyName} property is unavailable.");
        property.SetValue(entityMetadata, value);
    }
}
