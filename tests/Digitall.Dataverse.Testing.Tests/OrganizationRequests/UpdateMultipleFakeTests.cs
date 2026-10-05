// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class UpdateMultipleFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_MultipleEntities_UpdatesAll()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = id1, ["name"] = "Old 1" });
        _sut.Add(new Entity("account") { Id = id2, ["name"] = "Old 2" });

        var request = new UpdateMultipleRequest
        {
            Targets = new EntityCollection
            {
                Entities =
                {
                    new Entity("account") { Id = id1, ["name"] = "New 1" },
                    new Entity("account") { Id = id2, ["name"] = "New 2" }
                }
            }
        };

        var response = (UpdateMultipleResponse)_sut.Execute(request);

        await Assert.That(response).IsNotNull();
        await Assert.That(response.ResponseName).IsEqualTo("UpdateMultiple");

        var acc1 = _sut.Retrieve("account", id1, new ColumnSet("name"));
        var acc2 = _sut.Retrieve("account", id2, new ColumnSet("name"));
        await Assert.That(acc1.GetAttributeValue<string>("name")).IsEqualTo("New 1");
        await Assert.That(acc2.GetAttributeValue<string>("name")).IsEqualTo("New 2");
    }

    [Test]
    public async Task Execute_LaterTargetMissing_RollsBackEarlierUpdates()
    {
        var id = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = id, ["name"] = "Old" });

        var request = new UpdateMultipleRequest
        {
            Targets = new EntityCollection
            {
                EntityName = "account",
                Entities =
                {
                    new Entity("account") { Id = id, ["name"] = "New" },
                    new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Ghost" }
                }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();

        var account = _sut.Retrieve("account", id, new ColumnSet("name"));
        await Assert.That(account.GetAttributeValue<string>("name")).IsEqualTo("Old");
    }

    [Test]
    public async Task Execute_NonExistingRecord_ThrowsFaultException()
    {
        var request = new UpdateMultipleRequest
        {
            Targets = new EntityCollection
            {
                EntityName = "account",
                Entities = { new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Ghost" } }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_MixedEntityTypes_ThrowsFaultAndUpdatesNothing()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = id1, ["name"] = "Old" });
        _sut.Add(new Entity("contact") { Id = id2, ["lastname"] = "Old" });

        var request = new UpdateMultipleRequest
        {
            Targets = new EntityCollection
            {
                Entities =
                {
                    new Entity("account") { Id = id1, ["name"] = "New" },
                    new Entity("contact") { Id = id2, ["lastname"] = "New" }
                }
            }
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();

        var acc = _sut.Retrieve("account", id1, new ColumnSet("name"));
        await Assert.That(acc.GetAttributeValue<string>("name")).IsEqualTo("Old");
    }

    [Test]
    public async Task Execute_NullTargets_ThrowsFaultException()
    {
        var request = new UpdateMultipleRequest
        {
            Targets = null!
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNullException()
    {
        var fake = new UpdateMultipleFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
