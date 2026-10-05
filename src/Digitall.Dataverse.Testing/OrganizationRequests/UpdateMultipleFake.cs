// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class UpdateMultipleFake : OrganizationRequestFake<UpdateMultipleRequest, UpdateMultipleResponse>
{
    public override UpdateMultipleResponse Execute(UpdateMultipleRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        if (organizationRequest.Targets == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Targets' is missing");
        }

        foreach (var target in organizationRequest.Targets.Entities)
        {
            fakeOrganizationService.UpdateCore(target);
        }

        return new UpdateMultipleResponse
        {
            ResponseName = "UpdateMultiple"
        };
    }
}
