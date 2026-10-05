// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class ExecuteTransactionFake : OrganizationRequestFake<ExecuteTransactionRequest, ExecuteTransactionResponse>
{
    public override ExecuteTransactionResponse Execute(ExecuteTransactionRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);

        if (organizationRequest.Requests == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Requests' is missing");
        }

        return fakeOrganizationService.ExecuteAtomic(() =>
        {
            var response = new ExecuteTransactionResponse { ["Responses"] = new OrganizationResponseCollection() };

            foreach (var r in organizationRequest.Requests)
            {
                var result = fakeOrganizationService.Execute(r);

                if (organizationRequest.ReturnResponses.HasValue && organizationRequest.ReturnResponses.Value)
                {
                    response.Responses.Add(result);
                }
            }

            return response;
        });
    }
}
