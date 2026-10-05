// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Digitall.Dataverse.Testing.Extensions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests;

public class FakeDataverseBuilderTests
{
    [Test]
    public async Task GetOrganizationService_Should_Return_FakeOrganizationServiceAsync()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();

        await Assert.That(service).IsNotNull();
        await Assert.That(service).IsTypeOf<FakeOrganizationServiceAsync>();
    }

    [Test]
    public async Task FakeDataverseBuilder_With_Custom_TimeProvider()
    {
        var service = new FakeDataverseBuilder(new FakeTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))).GetOrganizationService();

        await Assert.That(service).IsNotNull();
        await Assert.That(service).IsTypeOf<FakeOrganizationServiceAsync>();
        await Assert.That(service.TimeProvider.GetUtcNow().Year).IsEqualTo(2000);
    }

    [Test]
    public async Task FakeDataverseBuilder_Should_Register_DefaultRequests()
    {
        var service = new FakeDataverseBuilder().GetOrganizationService();

        var result = service.Execute(new WhoAmIRequest());

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsTypeOf<WhoAmIResponse>();
    }

    [Test]
    public async Task AddData_Should_BeRetrieved_From_OrganizationService()
    {
        var entity = new Entity("unittest", Guid.NewGuid()) { ["name"] = "builder-record" };

        var service = new FakeDataverseBuilder()
            .AddData(entity)
            .GetOrganizationService();

        var retrieved = service.Retrieve(entity.LogicalName, entity.Id, new ColumnSet(true));

        await Assert.That(retrieved).IsNotNull();
        await Assert.That(retrieved).IsEquivalentTo(entity);
    }

    [Test]
    public async Task AddConfig_Should_Add_Definition_And_Value()
    {
        var service = new FakeDataverseBuilder()
            .AddConfig("dg_TestFlag", "default", "override")
            .GetOrganizationService();

        var definition = service.CreateQuery("environmentvariabledefinition").Single();
        var value = service.CreateQuery("environmentvariablevalue").Single();

        await Assert.That(definition.GetAttributeValue<string>("schemaname")).IsEqualTo("dg_TestFlag");
        await Assert.That(definition.GetAttributeValue<string>("defaultvalue")).IsEqualTo("default");
        await Assert.That(value.GetAttributeValue<string>("value")).IsEqualTo("override");
        await Assert.That(value.GetAttributeValue<EntityReference>("environmentvariabledefinitionid").Id).IsEqualTo(definition.Id);
    }

    [Test]
    public async Task WithUserId_Should_Set_UserId_On_OrganizationService()
    {
        var userId = Guid.NewGuid();

        var service = new FakeDataverseBuilder()
            .WithUserId(userId)
            .GetOrganizationService();

        await Assert.That(service.Options.UserId).IsEqualTo(userId);
    }

    [Test]
    public async Task WithBusinessUnitId_Should_Set_BusinessUnitId_On_OrganizationService()
    {
        var businessUnitId = Guid.NewGuid();

        var service = new FakeDataverseBuilder()
            .WithBusinessUnitId(businessUnitId)
            .GetOrganizationService();

        await Assert.That(service.Options.BusinessUnitId).IsEqualTo(businessUnitId);
    }

    [Test]
    public async Task WithFiscalYearStart_Should_Set_FiscalYearStart_On_OrganizationService()
    {
        var fiscalYearStart = new DateOnly(2024, 4, 1);

        var service = new FakeDataverseBuilder()
            .WithFiscalYearStart(fiscalYearStart)
            .GetOrganizationService();

        await Assert.That(service.Options.FiscalYearStart).IsEqualTo(fiscalYearStart);
    }

    [Test]
    public async Task WithOrganizationVersion_Should_Set_OrganizationVersion_On_OrganizationService()
    {
        var service = new FakeDataverseBuilder()
            .WithOrganizationVersion("9.2.1.1")
            .GetOrganizationService();

        await Assert.That(service.Options.OrganizationVersion).IsEqualTo("9.2.1.1");
    }

    [Test]
    public async Task WithOrganizationVersion_Should_Throw_On_Blank()
    {
        var builder = new FakeDataverseBuilder();

        await Assert.That(() => builder.WithOrganizationVersion(" ")).Throws<ArgumentException>();
    }

    [Test]
    public async Task WithOrganizationId_Should_Set_OrganizationId_On_OrganizationService()
    {
        var organizationId = Guid.NewGuid();

        var service = new FakeDataverseBuilder()
            .WithOrganizationId(organizationId)
            .GetOrganizationService();

        await Assert.That(service.Options.OrganizationId).IsEqualTo(organizationId);
    }

    [Test]
    public async Task WithOrganizationName_Should_Set_Names_On_OrganizationService()
    {
        var service = new FakeDataverseBuilder()
            .WithOrganizationName("contoso", "Contoso Ltd")
            .GetOrganizationService();

        await Assert.That(service.Options.OrganizationUniqueName).IsEqualTo("contoso");
        await Assert.That(service.Options.OrganizationFriendlyName).IsEqualTo("Contoso Ltd");
    }

    [Test]
    public async Task WithOrganizationName_Without_FriendlyName_Should_Keep_Default()
    {
        var service = new FakeDataverseBuilder()
            .WithOrganizationName("contoso")
            .GetOrganizationService();

        await Assert.That(service.Options.OrganizationUniqueName).IsEqualTo("contoso");
        await Assert.That(service.Options.OrganizationFriendlyName).IsEqualTo(new FakeDataverseOptions().OrganizationFriendlyName);
    }

    [Test]
    public async Task WithOrganizationName_Should_Throw_On_Blank_UniqueName()
    {
        var builder = new FakeDataverseBuilder();

        await Assert.That(() => builder.WithOrganizationName("")).Throws<ArgumentException>();
    }

    [Test]
    public async Task AddEntityMetadata_And_Relationships_Should_BeStored_In_Service()
    {
        var accountMetadata = new EntityMetadata
        {
            LogicalName = "account"
        };

        typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.ManyToManyRelationships))?
            .SetValue(accountMetadata, Array.Empty<ManyToManyRelationshipMetadata>());
        typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.OneToManyRelationships))?
            .SetValue(accountMetadata, Array.Empty<OneToManyRelationshipMetadata>());
        typeof(EntityMetadata).GetProperty(nameof(EntityMetadata.ManyToOneRelationships))?
            .SetValue(accountMetadata, Array.Empty<OneToManyRelationshipMetadata>());

        var relationship = new OneToManyRelationshipMetadata { SchemaName = "dg_account_contact" };

        var service = new FakeDataverseBuilder()
            .AddEntityMetadata(accountMetadata)
            .AddRelationships(relationship)
            .GetOrganizationService();

        await Assert.That(service.State.EntityMetadata.ContainsKey("account")).IsTrue();
        await Assert.That(service.State.Relationships.ContainsKey("dg_account_contact")).IsTrue();
    }
}
