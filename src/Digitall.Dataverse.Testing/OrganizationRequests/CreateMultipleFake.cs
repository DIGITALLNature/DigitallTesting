// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class CreateMultipleFake : OrganizationRequestFake<CreateMultipleRequest, CreateMultipleResponse>
{
    public override CreateMultipleResponse Execute(CreateMultipleRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        MultipleTargetsValidator.Validate(organizationRequest.Targets);

        var ids = fakeOrganizationService.ExecuteAtomic(() =>
        {
            var created = new List<Guid>();

            foreach (var target in organizationRequest.Targets.Entities)
            {
                var guid = fakeOrganizationService.CreateCore(target);

                // Deep insert: create sub-entities from RelatedEntities
                if (target.RelatedEntities.Count > 0)
                {
                    DeepInsertProcessor.Process(target.LogicalName, guid, target.RelatedEntities, fakeOrganizationService);
                }

                created.Add(guid);
            }

            return created;
        });

        return new CreateMultipleResponse
        {
            ResponseName = "CreateMultiple",
            Results = new ParameterCollection { { nameof(CreateMultipleResponse.Ids), ids.ToArray() } }
        };
    }
}
