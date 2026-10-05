// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class DisassociateFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        _sut.AddRequest(new AssociateFake());
        _sut.AddRequest(new DisassociateFake());
        _sut.AddRequest(new RetrieveMultipleFake());
        await Task.CompletedTask;
    }

    private void SetupManyToManyRelationship()
    {
        _sut.AddRelationship(new ManyToManyRelationshipMetadata
        {
            SchemaName = "account_contact_mm",
            Entity1LogicalName = "account",
            Entity2LogicalName = "contact",
            Entity1IntersectAttribute = "accountid",
            Entity2IntersectAttribute = "contactid",
            IntersectEntityName = "account_contact"
        });
        // Register intersect entity metadata so RetrieveMultiple in DisassociateFake doesn't reject it
        _sut.AddMetadata(new EntityMetadata { LogicalName = "account_contact" });
    }

    [Test]
    public async Task Execute_ManyToMany_RemovesIntersectRecord()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact") { Id = contactId });
        SetupManyToManyRelationship();

        // First associate
        _sut.Execute(new AssociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities = [new("contact", contactId)]
        });

        // Verify associated
        var before = _sut.CreateQuery("account_contact").ToList();
        await Assert.That(before).Count().IsEqualTo(1);

        // Disassociate
        _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities = [new("contact", contactId)]
        });

        var after = _sut.CreateQuery("account_contact").ToList();
        await Assert.That(after).IsEmpty();
    }

    [Test]
    public async Task Execute_ManyToMany_OnlyRemovesSpecificRelation()
    {
        var accountId = Guid.NewGuid();
        var contactId1 = Guid.NewGuid();
        var contactId2 = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact") { Id = contactId1 });
        _sut.Add(new Entity("contact") { Id = contactId2 });
        SetupManyToManyRelationship();

        // Associate both
        _sut.Execute(new AssociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities =
            [
                new("contact", contactId1),
                new("contact", contactId2)
            ]
        });

        // Disassociate only one
        _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities = [new("contact", contactId1)]
        });

        var remaining = _sut.CreateQuery("account_contact").ToList();
        await Assert.That(remaining).Count().IsEqualTo(1);
        await Assert.That(remaining[0].GetAttributeValue<Guid>("contactid")).IsEqualTo(contactId2);
    }

    [Test]
    public async Task Execute_UnknownRelationship_ThrowsFault()
    {
        var accountId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });

        void Action() => _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("nonexistent"),
            RelatedEntities = [new("contact", Guid.NewGuid())]
        });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.InvalidArgument);
    }

    [Test]
    public async Task Execute_OneToMany_ClearsLookupOnRelatedEntity()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact")
        {
            Id = contactId,
            ["parentcustomerid"] = new EntityReference("account", accountId)
        });

        _sut.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts_1n",
            ReferencedEntity = "account",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        });

        _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contacts_1n"),
            RelatedEntities = [new("contact", contactId)]
        });

        var contact = _sut.Retrieve("contact", contactId, new ColumnSet("parentcustomerid"));
        await Assert.That(contact.GetAttributeValue<EntityReference>("parentcustomerid")).IsNull();
    }

    [Test]
    public async Task Execute_OneToMany_FromReferencingSide_ClearsLookupOnTarget()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact")
        {
            Id = contactId,
            ["parentcustomerid"] = new EntityReference("account", accountId)
        });

        _sut.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts_1n",
            ReferencedEntity = "account",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        });

        _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("contact", contactId),
            Relationship = new Relationship("account_contacts_1n"),
            RelatedEntities = [new("account", accountId)]
        });

        var contact = _sut.Retrieve("contact", contactId, new ColumnSet("parentcustomerid"));
        await Assert.That(contact.GetAttributeValue<EntityReference>("parentcustomerid")).IsNull();
    }

    [Test]
    public async Task Execute_OneToMany_MultipleRelatedEntities_ClearsAll()
    {
        var accountId = Guid.NewGuid();
        var contactId1 = Guid.NewGuid();
        var contactId2 = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact") { Id = contactId1, ["parentcustomerid"] = new EntityReference("account", accountId) });
        _sut.Add(new Entity("contact") { Id = contactId2, ["parentcustomerid"] = new EntityReference("account", accountId) });

        _sut.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts_1n",
            ReferencedEntity = "account",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        });

        _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contacts_1n"),
            RelatedEntities =
            [
                new("contact", contactId1),
                new("contact", contactId2)
            ]
        });

        var c1 = _sut.Retrieve("contact", contactId1, new ColumnSet("parentcustomerid"));
        var c2 = _sut.Retrieve("contact", contactId2, new ColumnSet("parentcustomerid"));
        await Assert.That(c1.GetAttributeValue<EntityReference>("parentcustomerid")).IsNull();
        await Assert.That(c2.GetAttributeValue<EntityReference>("parentcustomerid")).IsNull();
    }

    [Test]
    public async Task Execute_OneToMany_MismatchedTargetEntity_ThrowsFault()
    {
        var leadId = Guid.NewGuid();
        _sut.Add(new Entity("lead") { Id = leadId });

        _sut.AddRelationship(new OneToManyRelationshipMetadata
        {
            SchemaName = "account_contacts_1n",
            ReferencedEntity = "account",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        });

        void Action() => _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("lead", leadId),
            Relationship = new Relationship("account_contacts_1n"),
            RelatedEntities = [new("contact", Guid.NewGuid())]
        });

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.InvalidArgument);
    }

    [Test]
    public async Task Execute_ReturnsDisassociateResponse()
    {
        var accountId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId });
        _sut.Add(new Entity("contact") { Id = contactId });
        SetupManyToManyRelationship();

        _sut.Execute(new AssociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities = [new("contact", contactId)]
        });

        var response = _sut.Execute(new DisassociateRequest
        {
            Target = new EntityReference("account", accountId),
            Relationship = new Relationship("account_contact_mm"),
            RelatedEntities = [new("contact", contactId)]
        });

        await Assert.That(response).IsTypeOf<DisassociateResponse>();
    }
}
