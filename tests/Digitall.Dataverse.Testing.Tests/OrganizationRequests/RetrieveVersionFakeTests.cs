// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Extensions;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveVersionFakeTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Execute_DefaultVersion_ReturnsExpectedVersion()
    {
        var request = new RetrieveVersionRequest();
        var response = (RetrieveVersionResponse)_sut.Execute(request);

        await Assert.That(response.Version).IsEqualTo("9.2.0.0");
    }

    [Test]
    public async Task Execute_CustomVersion_ReturnsCustomVersion()
    {
        _sut.Options.OrganizationVersion = "9.2.24051.00170";

        var request = new RetrieveVersionRequest();
        var response = (RetrieveVersionResponse)_sut.Execute(request);

        await Assert.That(response.Version).IsEqualTo("9.2.24051.00170");
    }

    [Test]
    public async Task Builder_WithOrganizationVersion_SetsOption()
    {
        var service = new FakeDataverseBuilder()
            .WithOrganizationVersion("9.1.0.1234")
            .GetOrganizationService();

        var response = (RetrieveVersionResponse)service.Execute(new RetrieveVersionRequest());

        await Assert.That(response.Version).IsEqualTo("9.1.0.1234");
    }

    [Test]
    public async Task Execute_NullRequest_ThrowsArgumentNullException()
    {
        var fake = new RetrieveVersionFake();
        await Assert.That(() => fake.Execute(null!, _sut))
            .Throws<ArgumentNullException>();
    }
}
