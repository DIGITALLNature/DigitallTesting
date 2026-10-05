// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class UpsertFake : OrganizationRequestFake<UpsertRequest, UpsertResponse>
{
    public override UpsertResponse Execute(UpsertRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);
        ArgumentNullException.ThrowIfNull(organizationRequest);

        // Dataverse serializes the request, so the caller's entity is never mutated
        var target = organizationRequest.Target.CloneEntity();
        var entityLogicalName = target.LogicalName;
        var entityId = target.Id;

        // Id takes precedence; alternate keys are only used when no Id is given
        var existingId = entityId != Guid.Empty
            ? fakeOrganizationService.EntityExists(entityLogicalName, entityId) ? entityId : null
            : fakeOrganizationService.FindEntityByAlternateKey(entityLogicalName, target.KeyAttributes)?.Id;

        var recordCreated = existingId is null;
        var keyAttributes = target.KeyAttributes.ToList();
        target.KeyAttributes.Clear();

        if (recordCreated)
        {
            foreach (var (key, value) in keyAttributes)
            {
                if (!target.Attributes.ContainsKey(key))
                {
                    target.Attributes[key] = value;
                }
            }

            // Create routes through CreateFake which handles deep insert
            entityId = fakeOrganizationService.Create(target);
        }
        else
        {
            entityId = existingId!.Value;
            target.Id = entityId;

            // The key used to address the record cannot be changed by the same request
            foreach (var (key, _) in keyAttributes)
            {
                target.Attributes.Remove(key);
            }

            fakeOrganizationService.Update(target);

            // Deep insert: sub-entities are always created even when the parent is updated
            if (target.RelatedEntities.Count > 0)
            {
                DeepInsertProcessor.Process(entityLogicalName, entityId, target.RelatedEntities, fakeOrganizationService);
            }
        }

        var result = new UpsertResponse();
        result.Results.Add("RecordCreated", recordCreated);
        result.Results.Add("Target", new EntityReference(entityLogicalName, entityId));
        return result;
    }
}
