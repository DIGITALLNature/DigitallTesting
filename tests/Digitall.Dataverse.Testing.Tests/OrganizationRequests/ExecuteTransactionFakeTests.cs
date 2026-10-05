// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class ExecuteTransactionFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        _sut.AddRequest(new ExecuteTransactionFake());
        _sut.AddRequest(new CreateFake());
        _sut.AddRequest(new UpdateFake());
        _sut.AddRequest(new DeleteFake());
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_MultipleCreates_AllEntitiesCreated()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "A" } },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "B" } },
                new CreateRequest { Target = new Entity("contact") { Id = Guid.NewGuid(), ["name"] = "C" } }
            ]
        };

        _sut.Execute(request);

        var accounts = _sut.CreateQuery("account").ToList();
        var contacts = _sut.CreateQuery("contact").ToList();
        await Assert.That(accounts).Count().IsEqualTo(2);
        await Assert.That(contacts).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_ReturnResponsesTrue_ResponsesCollectionPopulated()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } },
                new CreateRequest { Target = new Entity("contact") { Id = Guid.NewGuid() } }
            ],
            ReturnResponses = true
        };

        var response = (ExecuteTransactionResponse)_sut.Execute(request);

        await Assert.That(response.Responses).Count().IsEqualTo(2);
        await Assert.That(response.Responses[0]).IsTypeOf<CreateResponse>();
        await Assert.That(response.Responses[1]).IsTypeOf<CreateResponse>();
    }

    [Test]
    public async Task Execute_ReturnResponsesFalse_ResponsesCollectionEmpty()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } }
            ],
            ReturnResponses = false
        };

        var response = (ExecuteTransactionResponse)_sut.Execute(request);

        await Assert.That(response.Responses).IsEmpty();
    }

    [Test]
    public async Task Execute_MixedOperations_AllExecutedInOrder()
    {
        var accountId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = accountId, ["name"] = "Original" });

        var contactId = Guid.NewGuid();

        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new UpdateRequest { Target = new Entity("account") { Id = accountId, ["name"] = "Updated" } },
                new CreateRequest { Target = new Entity("contact") { Id = contactId, ["fullname"] = "New Contact" } }
            ]
        };

        _sut.Execute(request);

        var account = _sut.Retrieve("account", accountId, new ColumnSet(true));
        await Assert.That(account.GetAttributeValue<string>("name")).IsEqualTo("Updated");

        var contact = _sut.Retrieve("contact", contactId, new ColumnSet(true));
        await Assert.That(contact.GetAttributeValue<string>("fullname")).IsEqualTo("New Contact");
    }

    [Test]
    public async Task Execute_FailingRequest_ThrowsException()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } },
                new DeleteRequest { Target = new EntityReference("account", Guid.NewGuid()) } // Non-existing
            ]
        };

        void Action() => _sut.Execute(request);

        Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_FailingRequest_RollsBackAllOperations()
    {
        var accountId = Guid.NewGuid();
        var request = new ExecuteTransactionRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = accountId, ["name"] = "ShouldBeRolledBack" } },
                new DeleteRequest { Target = new EntityReference("account", Guid.NewGuid()) } // Non-existing
            ]
        };

        void Action() => _sut.Execute(request);

        Assert.Throws<FaultException<OrganizationServiceFault>>(Action);

        var exists = _sut.CreateQuery("account").Any(a => a.Id == accountId);
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task Execute_EmptyRequests_ReturnsEmptyResponse()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests = new OrganizationRequestCollection(),
            ReturnResponses = true
        };

        var response = (ExecuteTransactionResponse)_sut.Execute(request);

        await Assert.That(response.Responses).IsEmpty();
    }

    [Test]
    public async Task Execute_NullRequests_ThrowsFault()
    {
        var request = new ExecuteTransactionRequest
        {
            Requests = null!
        };

        void Action() => _sut.Execute(request);

        var ex = Assert.Throws<FaultException<OrganizationServiceFault>>(Action);
        await Assert.That(ex.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.InvalidArgument);
    }
}
