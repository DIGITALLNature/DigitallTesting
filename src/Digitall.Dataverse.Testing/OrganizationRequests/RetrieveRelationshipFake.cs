// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveRelationshipFake : OrganizationRequestFake<RetrieveRelationshipRequest, RetrieveRelationshipResponse>
{
    public override RetrieveRelationshipResponse Execute(RetrieveRelationshipRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        RelationshipMetadataBase? relationship = null;

        if (!string.IsNullOrWhiteSpace(organizationRequest.Name) &&
            fakeOrganizationService.State.Relationships.TryGetValue(organizationRequest.Name, out var foundByName))
        {
            relationship = foundByName;
        }
        else if (organizationRequest.MetadataId != Guid.Empty)
        {
            relationship = fakeOrganizationService.State.Relationships.Values
                .FirstOrDefault(r => r.MetadataId == organizationRequest.MetadataId);
        }

        if (relationship == null)
        {
            var identifier = !string.IsNullOrWhiteSpace(organizationRequest.Name)
                ? organizationRequest.Name
                : organizationRequest.MetadataId.ToString("D");
            ErrorFactory.ThrowFault(ErrorCodes.ObjectDoesNotExist, $"Relationship '{identifier}' does not exist");
        }

        return new RetrieveRelationshipResponse
        {
            ResponseName = "RetrieveRelationship",
            Results = new ParameterCollection
            {
                { nameof(RetrieveRelationshipResponse.RelationshipMetadata), relationship }
            }
        };
    }
}
