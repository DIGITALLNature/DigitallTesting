// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class ExecuteMultipleFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_MultipleCreates_AllEntitiesCreated()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "A" } },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "B" } },
                new CreateRequest { Target = new Entity("contact") { Id = Guid.NewGuid(), ["name"] = "C" } }
            ],
            Settings = new ExecuteMultipleSettings()
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
        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } },
                new CreateRequest { Target = new Entity("contact") { Id = Guid.NewGuid() } }
            ],
            Settings = new ExecuteMultipleSettings
            {
                ReturnResponses = true,
                ContinueOnError = true
            }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsFalse();
        await Assert.That(response.Responses).Count().IsEqualTo(2);
        await Assert.That(response.Responses[0].RequestIndex).IsEqualTo(0);
        await Assert.That(response.Responses[0].Response).IsTypeOf<CreateResponse>();
        await Assert.That(response.Responses[1].RequestIndex).IsEqualTo(1);
        await Assert.That(response.Responses[1].Response).IsTypeOf<CreateResponse>();
    }

    [Test]
    public async Task Execute_ReturnResponsesFalse_ResponsesCollectionEmptyOnSuccess()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } }
            ],
            Settings = new ExecuteMultipleSettings
            {
                ReturnResponses = false,
                ContinueOnError = true
            }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsFalse();
        await Assert.That(response.Responses).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Execute_WithError_ContinueOnErrorFalse_StopsAtFirstError()
    {
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = existingId });

        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "First" } },
                // Duplicate ID triggers fault
                new CreateRequest { Target = new Entity("account") { Id = existingId, ["name"] = "Duplicate" } },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Third" } }
            ],
            Settings = new ExecuteMultipleSettings
            {
                ReturnResponses = true,
                ContinueOnError = false
            }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsTrue();
        await Assert.That(response.Responses).Count().IsEqualTo(2);
        await Assert.That(response.Responses[0].Response).IsTypeOf<CreateResponse>();
        await Assert.That(response.Responses[1].Fault).IsNotNull();

        var accounts = _sut.CreateQuery("account").ToList();
        // Existing + First = 2, Third was never executed
        await Assert.That(accounts).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Execute_WithError_ContinueOnErrorTrue_ExecutesAllAndCollectsFaults()
    {
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account") { Id = existingId });

        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "First" } },
                // Duplicate ID triggers fault
                new CreateRequest { Target = new Entity("account") { Id = existingId, ["name"] = "Duplicate" } },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Third" } }
            ],
            Settings = new ExecuteMultipleSettings
            {
                ReturnResponses = true,
                ContinueOnError = true
            }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsTrue();
        await Assert.That(response.Responses).Count().IsEqualTo(3);
        await Assert.That(response.Responses[0].Response).IsTypeOf<CreateResponse>();
        await Assert.That(response.Responses[1].Fault).IsNotNull();
        await Assert.That(response.Responses[2].Response).IsTypeOf<CreateResponse>();

        var accounts = _sut.CreateQuery("account").ToList();
        // Existing + First + Third = 3
        await Assert.That(accounts).Count().IsEqualTo(3);
    }

    [Test]
    public async Task Execute_EmptyRequests_ReturnsEmptyResponse()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests = [],
            Settings = new ExecuteMultipleSettings()
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsFalse();
        await Assert.That(response.Responses).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Execute_NullRequests_ThrowsFaultException()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests = null!,
            Settings = new ExecuteMultipleSettings()
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task Execute_NullSettings_ThrowsFaultException()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests = [new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } }]
        };

        await Assert.That(() => _sut.Execute(request))
            .Throws<FaultException<OrganizationServiceFault>>();
        await Assert.That(_sut.CreateQuery("account").ToList()).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Execute_NestedExecuteMultiple_FaultsOnlyThatItem()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "First" } },
                new ExecuteMultipleRequest
                {
                    Requests = [],
                    Settings = new ExecuteMultipleSettings()
                },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Third" } }
            ],
            Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsTrue();
        await Assert.That(response.Responses).Count().IsEqualTo(3);
        await Assert.That(response.Responses[0].Response).IsTypeOf<CreateResponse>();
        await Assert.That(response.Responses[1].RequestIndex).IsEqualTo(1);
        await Assert.That(response.Responses[1].Fault).IsNotNull();
        await Assert.That(response.Responses[2].Response).IsTypeOf<CreateResponse>();
        await Assert.That(_sut.CreateQuery("account").ToList()).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Execute_NestedExecuteMultiple_ContinueOnErrorFalse_StopsAtNestedItem()
    {
        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } },
                new ExecuteMultipleRequest { Requests = [], Settings = new ExecuteMultipleSettings() },
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } }
            ],
            Settings = new ExecuteMultipleSettings { ContinueOnError = false, ReturnResponses = true }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsTrue();
        await Assert.That(response.Responses).Count().IsEqualTo(2);
        await Assert.That(_sut.CreateQuery("account").ToList()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Execute_NonFaultException_IsCapturedAsFault()
    {
        _sut.AddRequest(new ThrowingFake());

        var request = new ExecuteMultipleRequest
        {
            Requests =
            [
                new RetrieveVersionRequest(),
                new WhoAmIRequest(),
                new CreateRequest { Target = new Entity("account") { Id = Guid.NewGuid() } }
            ],
            Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = true }
        };

        var response = (ExecuteMultipleResponse)_sut.Execute(request);

        await Assert.That(response.IsFaulted).IsTrue();
        await Assert.That(response.Responses).Count().IsEqualTo(3);
        await Assert.That(response.Responses[0].Fault.Message).IsEqualTo("boom");
        await Assert.That(response.Responses[2].Response).IsTypeOf<CreateResponse>();
    }

    private sealed class ThrowingFake : OrganizationRequestFake<RetrieveVersionRequest, RetrieveVersionResponse>
    {
        public override RetrieveVersionResponse Execute(RetrieveVersionRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
            => throw new InvalidOperationException("boom");
    }

    [Test]
    public async Task Execute_NullOrganizationRequest_ThrowsArgumentNullException()
    {
        var fake = new ExecuteMultipleFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
