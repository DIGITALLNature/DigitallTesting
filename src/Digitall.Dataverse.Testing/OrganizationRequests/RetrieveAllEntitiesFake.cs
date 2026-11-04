// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Runtime.Serialization;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveAllEntitiesFake : OrganizationRequestFake<RetrieveAllEntitiesRequest, RetrieveAllEntitiesResponse>
{
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
        var projectedMetadata = CloneMetadata(metadata);

        if (!entityFilters.HasFlag(EntityFilters.Attributes))
        {
            SetMetadataProperty(projectedMetadata, nameof(EntityMetadata.Attributes), null);
        }

        if (!entityFilters.HasFlag(EntityFilters.Relationships))
        {
            SetMetadataProperty(projectedMetadata, nameof(EntityMetadata.ManyToManyRelationships), null);
            SetMetadataProperty(projectedMetadata, nameof(EntityMetadata.ManyToOneRelationships), null);
            SetMetadataProperty(projectedMetadata, nameof(EntityMetadata.OneToManyRelationships), null);
        }

        if (!entityFilters.HasFlag(EntityFilters.Privileges))
        {
            SetMetadataProperty(projectedMetadata, nameof(EntityMetadata.Privileges), null);
        }

        return projectedMetadata;
    }

    private static EntityMetadata CloneMetadata(EntityMetadata metadata)
    {
        using var stream = new MemoryStream();
        var serializer = new DataContractSerializer(typeof(EntityMetadata));
        serializer.WriteObject(stream, metadata);
        stream.Position = 0;
        return serializer.ReadObject(stream) as EntityMetadata
               ?? throw new InvalidOperationException("The registered entity metadata could not be cloned.");
    }

    private static void SetMetadataProperty(EntityMetadata metadata, string propertyName, object? value)
    {
        var property = typeof(EntityMetadata).GetProperty(propertyName)
                       ?? throw new InvalidOperationException($"The SDK EntityMetadata.{propertyName} property is unavailable.");
        property.SetValue(metadata, value);
    }
}
