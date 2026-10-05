// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class ExecuteMultipleFake : OrganizationRequestFake<ExecuteMultipleRequest, ExecuteMultipleResponse>
{
    public override ExecuteMultipleResponse Execute(ExecuteMultipleRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        if (organizationRequest.Settings == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Settings' is missing");
        }

        if (organizationRequest.Requests == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Requests' is missing");
        }

        if (organizationRequest.Requests.Any(r => r is ExecuteMultipleRequest))
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "ExecuteMultipleRequest cannot be nested inside another ExecuteMultipleRequest");
        }

        var continueOnError = organizationRequest.Settings.ContinueOnError;
        var returnResponses = organizationRequest.Settings.ReturnResponses;

        var responses = new ExecuteMultipleResponseItemCollection();
        var isFaulted = false;

        for (var i = 0; i < organizationRequest.Requests.Count; i++)
        {
            var request = organizationRequest.Requests[i];
            try
            {
                var response = fakeOrganizationService.Execute(request);
                if (returnResponses)
                {
                    responses.Add(new ExecuteMultipleResponseItem
                    {
                        RequestIndex = i,
                        Response = response
                    });
                }
            }
            catch (FaultException<OrganizationServiceFault> faultEx)
            {
                isFaulted = true;
                responses.Add(new ExecuteMultipleResponseItem
                {
                    RequestIndex = i,
                    Fault = faultEx.Detail
                });

                if (!continueOnError)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                isFaulted = true;
                responses.Add(new ExecuteMultipleResponseItem
                {
                    RequestIndex = i,
                    Fault = new OrganizationServiceFault { Message = ex.Message }
                });

                if (!continueOnError)
                {
                    break;
                }
            }
        }

        var result = new ExecuteMultipleResponse
        {
            ResponseName = "ExecuteMultiple",
            Results = new ParameterCollection
            {
                { nameof(ExecuteMultipleResponse.Responses), responses },
                { nameof(ExecuteMultipleResponse.IsFaulted), isFaulted }
            }
        };

        return result;
    }
}
