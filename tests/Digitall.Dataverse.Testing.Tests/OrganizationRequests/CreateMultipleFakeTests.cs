// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
                Entities =
                {
                    new Entity("account") { ["name"] = "Account 1" },
                    new Entity("account") { ["name"] = "Account 2" },
                    new Entity("contact") { ["firstname"] = "Jane" }
                }
            }
        };

        var response = (CreateMultipleResponse)_sut.Execute(request);

        await Assert.That(response.Ids).Count().IsEqualTo(3);
        await Assert.That(response.Ids[0]).IsNotEqualTo(Guid.Empty);
        await Assert.That(response.Ids[1]).IsNotEqualTo(Guid.Empty);
        await Assert.That(response.Ids[2]).IsNotEqualTo(Guid.Empty);

        var acc1 = _sut.Retrieve("account", response.Ids[0], new ColumnSet("name"));
        var acc2 = _sut.Retrieve("account", response.Ids[1], new ColumnSet("name"));
        var contact = _sut.Retrieve("contact", response.Ids[2], new ColumnSet("firstname"));

        await Assert.That(acc1.GetAttributeValue<string>("name")).IsEqualTo("Account 1");
        await Assert.That(acc2.GetAttributeValue<string>("name")).IsEqualTo("Account 2");
        await Assert.That(contact.GetAttributeValue<string>("firstname")).IsEqualTo("Jane");
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
