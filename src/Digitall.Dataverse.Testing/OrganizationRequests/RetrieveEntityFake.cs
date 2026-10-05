// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveEntityFake : OrganizationRequestFake<RetrieveEntityRequest, RetrieveEntityResponse>
{
    public override RetrieveEntityResponse Execute(RetrieveEntityRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);

        EntityMetadata? entityMetadata = null;

        if (!string.IsNullOrWhiteSpace(organizationRequest.LogicalName) &&
            fakeOrganizationService.State.EntityMetadata.TryGetValue(organizationRequest.LogicalName, out var foundByName))
        {
            entityMetadata = foundByName;
        }
        else if (organizationRequest.MetadataId != Guid.Empty)
        {
            entityMetadata = fakeOrganizationService.State.EntityMetadata.Values
                .FirstOrDefault(m => m.MetadataId == organizationRequest.MetadataId);
        }

        if (entityMetadata == null)
        {
            var identifier = !string.IsNullOrWhiteSpace(organizationRequest.LogicalName)
                ? organizationRequest.LogicalName
                : organizationRequest.MetadataId.ToString("D");
            ErrorFactory.ThrowFault(ErrorCodes.QueryBuilderNoEntity, $"The entity with a name = '{identifier}' with namemapping = 'Logical' was not found in the MetadataCache.");
        }

        var results = new ParameterCollection { { nameof(RetrieveEntityResponse.EntityMetadata), entityMetadata } };

        var response = new RetrieveEntityResponse { Results = results };
        return response;
    }
}
