// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

internal static class MultipleTargetsValidator
{
    // Dataverse requires all targets of a *Multiple request to share one entity type.
    public static void Validate(EntityCollection? targets)
    {
        if (targets == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Targets' is missing");
        }

        var expected = string.IsNullOrEmpty(targets.EntityName)
            ? targets.Entities.FirstOrDefault()?.LogicalName
            : targets.EntityName;

        foreach (var entity in targets.Entities)
        {
            if (entity.LogicalName != expected)
            {
                ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument,
                    $"All entities in 'Targets' must be of type '{expected}', but found '{entity.LogicalName}'");
            }
        }
    }
}
