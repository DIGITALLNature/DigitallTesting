// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class CreateMultipleFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_MultipleEntities_CreatesAllAndReturnsIds()
    {
        var request = new CreateMultipleRequest
        {
            Targets = new EntityCollection
            {
                EntityName = "account",
                Entities =
                {
                    new Entity("account") { ["name"] = "Account 1" },
                    new Entity("account") { ["name"] = "Account 2" }
                }
            }
        };

        var response = (CreateMultipleResponse)_sut.Execute(request);

        await Assert.That(response.Ids).Count().IsEqualTo(2);
        await Assert.That(response.Ids[0]).IsNotEqualTo(Guid.Empty);
        await Assert.That(response.Ids[1]).IsNotEqualTo(Guid.Empty);

        var acc1 = _sut.Retrieve("account", response.Ids[0], new ColumnSet("name"));
        var acc2 = _sut.Retrieve("account", response.Ids[1], new ColumnSet("name"));

        await Assert.That(acc1.GetAttributeValue<string>("name")).IsEqualTo("Account 1");
        await Assert.That(acc2.GetAttributeValue<string>("name")).IsEqualTo("Account 2");
    }

    [Test]
    public async Task Execute_MixedEntityTypes_ThrowsFaultAndCreatesNothing()
    {
        var request = new CreateMultipleRequest
        {
            Targets = new EntityCollection
            {
                Entities =
                {
                    new Entity("account") { ["name"] = "Account 1" },
                    new Entity("contact") { ["firstname"] = "Jane" }
                }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
        await Assert.That(_sut.CreateQuery("account").ToList()).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Execute_EntityNameMismatch_ThrowsFaultException()
    {
        var request = new CreateMultipleRequest
        {
            Targets = new EntityCollection
            {
                EntityName = "contact",
                Entities = { new Entity("account") { ["name"] = "Account 1" } }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_DuplicateId_ThrowsFaultException()
    {
        var id = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = id });

        var request = new CreateMultipleRequest
        {
            Targets = new EntityCollection
            {
                EntityName = "account",
                Entities = { new Entity("account") { Id = id } }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_WithRelatedEntities_PerformsDeepInsert()
    {
        _sut.State.Relationships["contact_customer_accounts"] = new OneToManyRelationshipMetadata
        {
            SchemaName = "contact_customer_accounts",
            ReferencedEntity = "account",
            ReferencedAttribute = "accountid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        };

        var account = new Entity("account") { ["name"] = "Contoso" };
        account.RelatedEntities[new Relationship("contact_customer_accounts")] =
            new EntityCollection([new Entity("contact") { ["lastname"] = "Smith" }]);

        var request = new CreateMultipleRequest
        {
            Targets = new EntityCollection { EntityName = "account", Entities = { account } }
        };

        _sut.Execute(request);

        await Assert.That(_sut.CreateQuery("contact").ToList()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_NullTargets_ThrowsFaultException()
    {
        var request = new CreateMultipleRequest
        {
            Targets = null!
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNullException()
    {
        var fake = new CreateMultipleFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
