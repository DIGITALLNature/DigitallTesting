// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Reflection;
using System.Runtime.Serialization;
using Digitall.Dataverse.Testing.Extensions;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveAllEntitiesFake : OrganizationRequestFake<RetrieveAllEntitiesRequest, RetrieveAllEntitiesResponse>
{
    private static readonly PropertyInfo[] s_metadataProperties = typeof(EntityMetadata)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public);

    public override RetrieveAllEntitiesResponse Execute(RetrieveAllEntitiesRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        const EntityFilters supportedFilters = EntityFilters.Entity
                                               | EntityFilters.Attributes
                                               | EntityFilters.Relationships
                                               | EntityFilters.Privileges;
        if ((organizationRequest.EntityFilters & ~supportedFilters) != 0)
        {
            throw new NotSupportedException($"EntityFilters value '{organizationRequest.EntityFilters}' is not supported.");
        }

        var knownMetadata = fakeOrganizationService.State.EntityMetadata.Values
            .Select(metadata => ProjectMetadata(metadata, organizationRequest.EntityFilters))
            .ToArray();
        return new RetrieveAllEntitiesResponse
        {
            Results =
            {
                ["EntityMetadata"] = knownMetadata
            }
        };
    }

    private static EntityMetadata ProjectMetadata(EntityMetadata metadata, EntityFilters entityFilters)
    {
        var projectedMetadata = new EntityMetadata();
        foreach (var property in s_metadataProperties)
        {
            if (property.Name == nameof(IExtensibleDataObject.ExtensionData)
                || IsExcludedCollection(property.Name, entityFilters)
                || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var setter = property.GetSetMethod(nonPublic: true);
            if (setter == null)
            {
                continue;
            }

            setter.Invoke(projectedMetadata, [property.GetValue(metadata).DeepClone()]);
        }

        return projectedMetadata;
    }

    private static bool IsExcludedCollection(string propertyName, EntityFilters entityFilters)
    {
        return (!entityFilters.HasFlag(EntityFilters.Attributes)
                && propertyName == nameof(EntityMetadata.Attributes))
               || (!entityFilters.HasFlag(EntityFilters.Relationships)
                   && propertyName is nameof(EntityMetadata.ManyToManyRelationships)
                       or nameof(EntityMetadata.ManyToOneRelationships)
                       or nameof(EntityMetadata.OneToManyRelationships))
               || (!entityFilters.HasFlag(EntityFilters.Privileges)
                   && propertyName == nameof(EntityMetadata.Privileges));
    }
}
