// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Collections;
using System.Reflection;
using Digitall.Dataverse.Testing.Extensions;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

// ReSharper disable once UnusedMember.Global : Registered automatically through reflection.
public class RetrieveMetadataChangesFake : OrganizationRequestFake<RetrieveMetadataChangesRequest, RetrieveMetadataChangesResponse>
{
    private static readonly string[] s_entityCollectionProperties =
    [
        nameof(EntityMetadata.Attributes),
        nameof(EntityMetadata.OneToManyRelationships),
        nameof(EntityMetadata.ManyToOneRelationships),
        nameof(EntityMetadata.ManyToManyRelationships),
        nameof(EntityMetadata.Keys)
    ];

    private static readonly string[] s_entityProperties =
    [
        nameof(MetadataBase.MetadataId),
        nameof(EntityMetadata.LogicalName),
        nameof(EntityMetadata.SchemaName),
        nameof(EntityMetadata.DisplayName),
        nameof(EntityMetadata.IsManaged),
        nameof(EntityMetadata.IsCustomEntity),
        nameof(EntityMetadata.IsAuditEnabled),
        nameof(EntityMetadata.OwnershipType),
        .. s_entityCollectionProperties
    ];

    private static readonly string[] s_attributeProperties =
    [
        nameof(MetadataBase.MetadataId),
        nameof(AttributeMetadata.LogicalName),
        nameof(AttributeMetadata.SchemaName),
        nameof(AttributeMetadata.DisplayName),
        nameof(AttributeMetadata.AttributeType),
        nameof(AttributeMetadata.IsManaged)
    ];

    private static readonly string[] s_relationshipProperties =
    [
        nameof(MetadataBase.MetadataId),
        nameof(RelationshipMetadataBase.SchemaName),
        nameof(RelationshipMetadataBase.RelationshipType),
        nameof(RelationshipMetadataBase.IsManaged)
    ];

    private static readonly string[] s_keyProperties =
    [
        nameof(MetadataBase.MetadataId),
        nameof(EntityKeyMetadata.LogicalName),
        nameof(EntityKeyMetadata.SchemaName),
        nameof(EntityKeyMetadata.KeyAttributes),
        nameof(EntityKeyMetadata.EntityKeyIndexStatus),
        nameof(EntityKeyMetadata.IsManaged)
    ];

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
        var entityProperties = ValidateProperties(query.Properties, "entity", s_entityProperties);
        if (ContainsProperty(entityProperties, nameof(EntityMetadata.Attributes)) && query.AttributeQuery == null)
        {
            throw new NotSupportedException("An AttributeQuery with explicit properties is required when entity Attributes are requested.");
        }

        var requestsRelationships = ContainsProperty(entityProperties, nameof(EntityMetadata.OneToManyRelationships))
                                    || ContainsProperty(entityProperties, nameof(EntityMetadata.ManyToOneRelationships))
                                    || ContainsProperty(entityProperties, nameof(EntityMetadata.ManyToManyRelationships));
        if (requestsRelationships && query.RelationshipQuery == null)
        {
            throw new NotSupportedException("A RelationshipQuery with explicit properties is required when entity relationships are requested.");
        }

        var requestsKeys = ContainsProperty(entityProperties, nameof(EntityMetadata.Keys));
        if (requestsKeys && query.KeyQuery == null)
        {
            throw new NotSupportedException("A KeyQuery with explicit properties is required when entity Keys are requested.");
        }

        var entityMatches = CreateMetadataIdPredicate(query.Criteria);
        var attributeMatches = query.AttributeQuery == null
            ? null
            : CreateMetadataIdPredicate(query.AttributeQuery.Criteria);
        var relationshipMatches = query.RelationshipQuery == null
            ? null
            : CreateMetadataIdPredicate(query.RelationshipQuery.Criteria);
        var keyMatches = query.KeyQuery == null
            ? null
            : CreateMetadataIdPredicate(query.KeyQuery.Criteria);

        var attributeProperties = query.AttributeQuery == null
            ? null
            : ValidateProperties(query.AttributeQuery.Properties, "attribute", s_attributeProperties);
        var relationshipProperties = query.RelationshipQuery == null
            ? null
            : ValidateProperties(query.RelationshipQuery.Properties, "relationship", s_relationshipProperties);
        var keyProperties = query.KeyQuery == null
            ? null
            : ValidateProperties(query.KeyQuery.Properties, "key", s_keyProperties);

        if (query.LabelQuery != null)
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake does not support label metadata queries.");
        }

        var projectedMetadata = new EntityMetadataCollection();
        foreach (var metadata in fakeOrganizationService.State.EntityMetadata.Values.Where(metadata => entityMatches(metadata)))
        {
            projectedMetadata.Add(ProjectEntityMetadata(
                metadata,
                entityProperties,
                query,
                attributeProperties,
                attributeMatches,
                relationshipProperties,
                relationshipMatches,
                keyProperties,
                keyMatches));
        }

        return new RetrieveMetadataChangesResponse
        {
            Results =
            {
                ["EntityMetadata"] = projectedMetadata
            }
        };
    }

    private static IReadOnlyCollection<string> ValidateProperties(
        MetadataPropertiesExpression? properties,
        string metadataType,
        IReadOnlyCollection<string> supportedProperties)
    {
        if (properties == null || properties.AllProperties)
        {
            throw new NotSupportedException($"RetrieveMetadataChangesFake requires explicit {metadataType} metadata properties.");
        }

        if (properties.PropertyNames.Any(property =>
                !supportedProperties.Contains(property, StringComparer.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException($"RetrieveMetadataChangesFake contains unsupported {metadataType} metadata properties.");
        }

        return properties.PropertyNames.ToArray();
    }

    private static Func<MetadataBase, bool> CreateMetadataIdPredicate(MetadataFilterExpression? criteria)
    {
        if (criteria == null)
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake requires explicit metadata criteria.");
        }

        var predicates = criteria.Conditions
            .Select(CreateConditionPredicate)
            .Concat(criteria.Filters.Select(CreateMetadataIdPredicate))
            .ToArray();

        return criteria.FilterOperator switch
        {
            Microsoft.Xrm.Sdk.Query.LogicalOperator.And => metadata => predicates.All(predicate => predicate(metadata)),
            Microsoft.Xrm.Sdk.Query.LogicalOperator.Or => metadata => predicates.Any(predicate => predicate(metadata)),
            _ => throw new NotSupportedException($"Metadata filter operator '{criteria.FilterOperator}' is not supported.")
        };
    }

    private static Func<MetadataBase, bool> CreateConditionPredicate(MetadataConditionExpression condition)
    {
        if (!string.Equals(condition.PropertyName, nameof(MetadataBase.MetadataId), StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("RetrieveMetadataChangesFake supports only MetadataId filter conditions.");
        }

        return condition.ConditionOperator switch
        {
            MetadataConditionOperator.Equals when condition.Value is Guid metadataId =>
                metadata => metadata.MetadataId == metadataId,
            MetadataConditionOperator.In =>
                CreateInPredicate(condition.Value),
            _ => throw new NotSupportedException(
                $"Metadata condition operator '{condition.ConditionOperator}' or its value is not supported.")
        };
    }

    private static Func<MetadataBase, bool> CreateInPredicate(object? value)
    {
        if (value is not IEnumerable values || value is string)
        {
            throw new NotSupportedException("MetadataId In criteria require a list of Guid values.");
        }

        var metadataIds = new HashSet<Guid>();
        foreach (var item in values)
        {
            if (item is not Guid metadataId)
            {
                throw new NotSupportedException("MetadataId In criteria require a list of Guid values.");
            }

            metadataIds.Add(metadataId);
        }

        return metadata => metadata.MetadataId.HasValue && metadataIds.Contains(metadata.MetadataId.Value);
    }

    private static EntityMetadata ProjectEntityMetadata(
        EntityMetadata metadata,
        IReadOnlyCollection<string> entityProperties,
        EntityQueryExpression query,
        IReadOnlyCollection<string>? attributeProperties,
        Func<MetadataBase, bool>? attributeMatches,
        IReadOnlyCollection<string>? relationshipProperties,
        Func<MetadataBase, bool>? relationshipMatches,
        IReadOnlyCollection<string>? keyProperties,
        Func<MetadataBase, bool>? keyMatches)
    {
        var scalarProperties = entityProperties
            .Where(property => !s_entityCollectionProperties.Contains(property, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var projection = ProjectMetadata(metadata, scalarProperties);

        if (query.AttributeQuery != null)
        {
            var attributes = metadata.Attributes?
                .Where(attribute => attributeMatches!(attribute))
                .Select(attribute => ProjectMetadata(attribute, attributeProperties!))
                .ToArray() ?? [];
            SetPropertyValue(projection, nameof(EntityMetadata.Attributes), attributes);
        }

        if (query.RelationshipQuery != null)
        {
            var oneToManyRelationships = metadata.OneToManyRelationships?
                .Where(relationship => relationshipMatches!(relationship))
                .Select(relationship => ProjectMetadata(relationship, relationshipProperties!))
                .ToArray() ?? [];
            var manyToManyRelationships = metadata.ManyToManyRelationships?
                .Where(relationship => relationshipMatches!(relationship))
                .Select(relationship => ProjectMetadata(relationship, relationshipProperties!))
                .ToArray() ?? [];
            var manyToOneRelationships = metadata.ManyToOneRelationships?
                .Where(relationship => relationshipMatches!(relationship))
                .Select(relationship => ProjectMetadata(relationship, relationshipProperties!))
                .ToArray() ?? [];

            SetPropertyValue(projection, nameof(EntityMetadata.OneToManyRelationships), oneToManyRelationships);
            SetPropertyValue(projection, nameof(EntityMetadata.ManyToManyRelationships), manyToManyRelationships);
            SetPropertyValue(projection, nameof(EntityMetadata.ManyToOneRelationships), manyToOneRelationships);
        }

        if (query.KeyQuery != null)
        {
            var keys = metadata.Keys?
                .Where(key => keyMatches!(key))
                .Select(key => ProjectMetadata(key, keyProperties!))
                .ToArray() ?? [];
            SetPropertyValue(projection, nameof(EntityMetadata.Keys), keys);
        }

        return projection;
    }

    private static TMetadata ProjectMetadata<TMetadata>(
        TMetadata metadata,
        IReadOnlyCollection<string> properties)
        where TMetadata : MetadataBase
    {
        var projection = (TMetadata)(Activator.CreateInstance(metadata.GetType())
                                     ?? throw new InvalidOperationException(
                                         $"Could not create metadata projection for '{metadata.GetType().Name}'."));

        foreach (var propertyName in properties)
        {
            var property = metadata.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException(
                    $"The SDK metadata property '{metadata.GetType().Name}.{propertyName}' is unavailable.");
            var value = property.GetValue(metadata).DeepClone();
            SetPropertyValue(projection, property.Name, value);
        }

        return projection;
    }

    private static void SetPropertyValue(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                       ?? throw new InvalidOperationException(
                           $"The SDK metadata property '{target.GetType().Name}.{propertyName}' is unavailable.");
        var setter = property.GetSetMethod(nonPublic: true)
                     ?? throw new InvalidOperationException(
                         $"The SDK metadata property '{target.GetType().Name}.{propertyName}' cannot be set.");
        setter.Invoke(target, [value]);
    }

    private static bool ContainsProperty(IEnumerable<string> properties, string propertyName)
    {
        return properties.Any(property => string.Equals(property, propertyName, StringComparison.OrdinalIgnoreCase));
    }
}
