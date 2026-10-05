// Copyright (c) DIGITALL Nature.All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class WhoAmIFake : OrganizationRequestFake<WhoAmIRequest, WhoAmIResponse>
{
    public override WhoAmIResponse Execute(WhoAmIRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        var userId = fakeOrganizationService.Options.UserId;

        var results = new ParameterCollection { { "UserId", userId } };

        var user = fakeOrganizationService.CreateQuery("systemuser").SingleOrDefault(u => u.Id == userId);

        if (user != null)
        {
            var buId = GetBusinessUnitId(user) ?? fakeOrganizationService.Options.BusinessUnitId;
            results.Add("BusinessUnitId", buId);

            var orgId = GetOrganizationId(fakeOrganizationService, user, buId);
            if (orgId == Guid.Empty)
            {
                orgId = fakeOrganizationService.Options.OrganizationId;
            }
            results.Add("OrganizationId", orgId);
        }
        else if (fakeOrganizationService.Options.OrganizationId != Guid.Empty)
        {
            results.Add("OrganizationId", fakeOrganizationService.Options.OrganizationId);
        }

        var response = new WhoAmIResponse { Results = results };
        return response;
    }

    private static Guid? GetBusinessUnitId(Entity user)
    {
        var buRef = user.GetAttributeValue<EntityReference>("businessunitid");
        var buId = buRef?.Id;
        return buId;
    }

    private static Guid GetOrganizationId(FakeOrganizationService state, Entity user, Guid buId)
    {
        var orgId = user.GetAttributeValue<Guid?>("organizationid") ?? Guid.Empty;
        if (orgId != Guid.Empty) return orgId;

        var bu = state.CreateQuery("businessunit").SingleOrDefault(b => b.Id == buId);
        var orgRef = bu?.GetAttributeValue<EntityReference>("organizationid");
        orgId = orgRef?.Id ?? Guid.Empty;

        return orgId;
    }
}
