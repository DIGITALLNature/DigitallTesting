// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Errors;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class DisassociateFake : OrganizationRequestFake<DisassociateRequest, DisassociateResponse>
{
    public override DisassociateResponse Execute(DisassociateRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);
        ArgumentNullException.ThrowIfNull(fakeOrganizationService);

        var entityName = organizationRequest.Target.LogicalName;
        var entityId = organizationRequest.Target.Id;
        var relationship = organizationRequest.Relationship;
        var relatedEntities = organizationRequest.RelatedEntities;

        var relationshipMetadata = fakeOrganizationService.GetRelationship(relationship.SchemaName);

        if (relationshipMetadata == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, $"Relationship {relationship.SchemaName} does not exist in the metadata cache");
        }

        foreach (var relatedEntity in relatedEntities)
        {
            switch (relationshipMetadata)
            {
                case ManyToManyRelationshipMetadata manyToManyRelationshipMetadata:
                    {
                        var isFrom1To2 = entityName == manyToManyRelationshipMetadata.Entity1LogicalName;
                        var fromAttribute = isFrom1To2 ? manyToManyRelationshipMetadata.Entity1IntersectAttribute : manyToManyRelationshipMetadata.Entity2IntersectAttribute;
                        var toAttribute = isFrom1To2 ? manyToManyRelationshipMetadata.Entity2IntersectAttribute : manyToManyRelationshipMetadata.Entity1IntersectAttribute;

                        var query = new QueryExpression(manyToManyRelationshipMetadata.IntersectEntityName) { ColumnSet = new ColumnSet(true), Criteria = new FilterExpression(LogicalOperator.And) };

                        query.Criteria.AddCondition(new ConditionExpression(fromAttribute, ConditionOperator.Equal, entityId));
                        query.Criteria.AddCondition(new ConditionExpression(toAttribute, ConditionOperator.Equal, relatedEntity.Id));

                        var results = fakeOrganizationService.RetrieveMultiple(query);

                        if (results.Entities.Count == 1)
                        {
                            fakeOrganizationService.Delete(manyToManyRelationshipMetadata.IntersectEntityName, results.Entities.First().Id);
                        }
                        break;
                    }
                case OneToManyRelationshipMetadata oneToMany:
                    {
                        if (entityName == oneToMany.ReferencedEntity)
                        {
                            var entityToUpdate = new Entity(relatedEntity.LogicalName, relatedEntity.Id)
                            {
                                [oneToMany.ReferencingAttribute] = null
                            };
                            fakeOrganizationService.Update(entityToUpdate);
                        }
                        else if (entityName == oneToMany.ReferencingEntity)
                        {
                            var entityToUpdate = new Entity(entityName, entityId)
                            {
                                [oneToMany.ReferencingAttribute] = null
                            };
                            fakeOrganizationService.Update(entityToUpdate);
                        }
                        else
                        {
                            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument,
                                $"Relationship '{oneToMany.SchemaName}' is between '{oneToMany.ReferencedEntity}' and '{oneToMany.ReferencingEntity}', but target entity is '{entityName}'");
                        }
                        break;
                    }
                default:
                    ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument,
                        $"Relationship metadata type '{relationshipMetadata.GetType().Name}' is not supported for Disassociate");
                    break;
            }
        }

        return new DisassociateResponse();
    }
}
