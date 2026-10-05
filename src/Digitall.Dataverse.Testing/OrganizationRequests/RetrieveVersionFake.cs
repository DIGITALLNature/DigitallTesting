// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveVersionFake : OrganizationRequestFake<RetrieveVersionRequest, RetrieveVersionResponse>
{
    public override RetrieveVersionResponse Execute(RetrieveVersionRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        return new RetrieveVersionResponse
        {
            ResponseName = "RetrieveVersion",
            Results = new ParameterCollection
            {
                { nameof(RetrieveVersionResponse.Version), fakeOrganizationService.Options.OrganizationVersion }
            }
        };
    }
}
