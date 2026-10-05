// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveAttributeFake : OrganizationRequestFake<RetrieveAttributeRequest, RetrieveAttributeResponse>
{
    public override RetrieveAttributeResponse Execute(RetrieveAttributeRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);

        if (!fakeOrganizationService.State.EntityMetadata.TryGetValue(organizationRequest.EntityLogicalName, out var entityMetadata))
        {
            ErrorFactory.ThrowFault(ErrorCodes.QueryBuilderNoEntity, $"The entity with a name = '{organizationRequest.EntityLogicalName}' with namemapping = 'Logical' was not found in the MetadataCache.");
        }

        var attributeMetadata = entityMetadata.Attributes?.FirstOrDefault(a => a.LogicalName == organizationRequest.LogicalName);

        if (attributeMetadata == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.QueryBuilderNoAttribute, $"The attribute {organizationRequest.LogicalName} does not exist on this entity.");
        }

        var results = new ParameterCollection { { nameof(RetrieveAttributeResponse.AttributeMetadata), attributeMetadata } };

        var response = new RetrieveAttributeResponse { Results = results };
        return response;
    }
}
