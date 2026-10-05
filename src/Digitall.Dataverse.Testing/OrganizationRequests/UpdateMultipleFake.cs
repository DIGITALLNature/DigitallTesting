// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class UpdateMultipleFake : OrganizationRequestFake<UpdateMultipleRequest, UpdateMultipleResponse>
{
    public override UpdateMultipleResponse Execute(UpdateMultipleRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        MultipleTargetsValidator.Validate(organizationRequest.Targets);

        fakeOrganizationService.ExecuteAtomic(() =>
        {
            foreach (var target in organizationRequest.Targets.Entities)
            {
                fakeOrganizationService.UpdateCore(target);
            }

            return true;
        });

        return new UpdateMultipleResponse
        {
            ResponseName = "UpdateMultiple"
        };
    }
}
