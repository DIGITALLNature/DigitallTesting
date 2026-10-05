// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using OrganizationDetail = Microsoft.Xrm.Sdk.Organization.OrganizationDetail;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveCurrentOrganizationFake : OrganizationRequestFake<RetrieveCurrentOrganizationRequest, RetrieveCurrentOrganizationResponse>
{
    public override RetrieveCurrentOrganizationResponse Execute(RetrieveCurrentOrganizationRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        var orgEntity = fakeOrganizationService.CreateQuery("organization").FirstOrDefault();

        var orgId = fakeOrganizationService.Options.OrganizationId != Guid.Empty
            ? fakeOrganizationService.Options.OrganizationId
            : orgEntity?.Id ?? Guid.Empty;

        var uniqueName = orgEntity?.GetAttributeValue<string>("uniquename")
                         ?? fakeOrganizationService.Options.OrganizationUniqueName;

        var friendlyName = orgEntity?.GetAttributeValue<string>("name")
                          ?? fakeOrganizationService.Options.OrganizationFriendlyName;

        var detail = new OrganizationDetail
        {
            OrganizationId = orgId,
            UniqueName = uniqueName,
            FriendlyName = friendlyName,
            UrlName = uniqueName,
            OrganizationVersion = fakeOrganizationService.Options.OrganizationVersion
        };

        return new RetrieveCurrentOrganizationResponse
        {
            ResponseName = "RetrieveCurrentOrganization",
            Results = new ParameterCollection
            {
                { nameof(RetrieveCurrentOrganizationResponse.Detail), detail }
            }
        };
    }
}
