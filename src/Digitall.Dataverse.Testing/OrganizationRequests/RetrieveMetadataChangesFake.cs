// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveMetadataChangesFake : OrganizationRequestFake<RetrieveMetadataChangesRequest, RetrieveMetadataChangesResponse>
{
    public override RetrieveMetadataChangesResponse Execute(
        RetrieveMetadataChangesRequest organizationRequest,
        FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(organizationRequest.Query);

        if (!string.IsNullOrEmpty(organizationRequest.ClientVersionStamp))
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake does not support ClientVersionStamp or delta queries.");
        }

        var query = organizationRequest.Query;
        ValidateQuery(query);

        var projectedMetadata = new EntityMetadataCollection();
        foreach (var metadata in fakeOrganizationService.State.EntityMetadata.Values.Where(MatchesCriteria(query.Criteria)))
        {
            projectedMetadata.Add(ProjectEntityMetadata(metadata, query));
        }

        return new RetrieveMetadataChangesResponse
        {
            Results =
            {
                ["EntityMetadata"] = projectedMetadata
            }
        };
    }

    private static void ValidateQuery(EntityQueryExpression query)
    {
        if (query.AttributeQuery != null || query.RelationshipQuery != null || query.LabelQuery != null)
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake supports entity and key metadata queries only.");
        }

        var entityProperties = query.Properties;
        if (entityProperties == null || entityProperties.AllProperties)
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake requires explicit entity metadata properties.");
        }

        var propertyNames = entityProperties.PropertyNames;
        if (propertyNames.Any(property =>
                !string.Equals(property, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(property, nameof(EntityMetadata.Keys), StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake supports only entity MetadataId and Keys properties.");
        }

        var requestsKeys = propertyNames.Any(property =>
            string.Equals(property, nameof(EntityMetadata.Keys), StringComparison.OrdinalIgnoreCase));
        if (requestsKeys && query.KeyQuery == null)
        {
            throw new NotSupportedException("A KeyQuery with explicit properties is required when entity Keys are requested.");
        }

        if (!requestsKeys && query.KeyQuery != null)
        {
            throw new NotSupportedException("A KeyQuery requires the entity Keys property to be requested.");
        }

        if (query.KeyQuery != null)
        {
            var keyProperties = query.KeyQuery.Properties;
            if (keyProperties == null || keyProperties.AllProperties)
            {
                throw new NotSupportedException("RetrieveMetadataChangesFake requires explicit key metadata properties.");
            }

            if (keyProperties.PropertyNames.Any(property =>
                    !string.Equals(property, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(property, nameof(EntityKeyMetadata.LogicalName), StringComparison.OrdinalIgnoreCase)))
            {
                throw new NotSupportedException("RetrieveMetadataChangesFake supports only key MetadataId and LogicalName properties.");
            }
        }

        if (query.Criteria.FilterOperator != LogicalOperator.And || query.Criteria.Filters.Count != 0)
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake supports flat AND metadata criteria only.");
        }

        foreach (var condition in query.Criteria.Conditions)
        {
            if (!string.Equals(condition.PropertyName, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase)
                || condition.ConditionOperator != MetadataConditionOperator.Equals
                || condition.Value is not Guid)
            {
                throw new NotSupportedException("RetrieveMetadataChangesFake supports only MetadataId Equals {Guid} criteria.");
            }
        }
    }

    private static Func<EntityMetadata, bool> MatchesCriteria(MetadataFilterExpression criteria)
    {
        var metadataIds = criteria.Conditions.Select(condition => (Guid)condition.Value).ToArray();
        return metadata => metadataIds.All(metadataId => metadata.MetadataId == metadataId);
    }

    private static EntityMetadata ProjectEntityMetadata(EntityMetadata metadata, EntityQueryExpression query)
    {
        var projection = new EntityMetadata();
        var entityProperties = query.Properties.PropertyNames;

        if (entityProperties.Any(property =>
                string.Equals(property, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase)))
        {
            projection.MetadataId = metadata.MetadataId;
        }

        if (entityProperties.Any(property =>
                string.Equals(property, nameof(EntityMetadata.Keys), StringComparison.OrdinalIgnoreCase)))
        {
            var keyProperties = query.KeyQuery!.Properties.PropertyNames;
            var keys = metadata.Keys?.Select(key =>
                {
                    var keyProjection = new EntityKeyMetadata();
                    if (keyProperties.Any(property =>
                            string.Equals(property, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase)))
                    {
                        keyProjection.MetadataId = key.MetadataId;
                    }

                    if (keyProperties.Any(property =>
                            string.Equals(property, nameof(EntityKeyMetadata.LogicalName), StringComparison.OrdinalIgnoreCase)))
                    {
                        keyProjection.LogicalName = key.LogicalName;
                    }

                    return keyProjection;
                })
                .ToArray() ?? [];
            var keysProperty = typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.Keys))
                               ?? throw new InvalidOperationException("The SDK EntityMetadata.Keys property is unavailable.");
            keysProperty.SetValue(projection, keys);
        }

        return projection;
    }
}
