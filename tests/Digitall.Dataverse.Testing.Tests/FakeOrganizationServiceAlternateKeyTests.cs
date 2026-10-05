// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests;

public class FakeOrganizationServiceAlternateKeyTests
{
    private readonly FakeOrganizationService _sut = new();

    [Test]
    public async Task RetrieveWithAlternateKey_CompositeKey_ReturnsMatchingRecord()
    {
        // Arrange
        var berlinId = Guid.NewGuid();
        _sut.Add(new Entity("account", berlinId) { ["accountnumber"] = "ACC-001", ["address1_city"] = "Berlin" });
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = "ACC-001", ["address1_city"] = "London" });

        var keys = new KeyAttributeCollection { { "accountnumber", "ACC-001" }, { "address1_city", "Berlin" } };

        // Act
        var result = _sut.RetrieveWithAlternateKey("account", keys, new ColumnSet(true));

        // Assert
        await Assert.That(result.Id).IsEqualTo(berlinId);
    }

    [Test]
    public async Task RetrieveWithAlternateKey_StringKey_IsCaseInsensitive()
    {
        // Arrange
        var id = Guid.NewGuid();
        _sut.Add(new Entity("account", id) { ["accountnumber"] = "acc-001" });

        // Act
        var result = _sut.RetrieveWithAlternateKey("account", new KeyAttributeCollection { { "accountnumber", "ACC-001" } }, new ColumnSet(true));

        // Assert
        await Assert.That(result.Id).IsEqualTo(id);
    }

    [Test]
    public async Task RetrieveWithAlternateKey_NoMatch_ThrowsObjectDoesNotExist()
    {
        // Arrange
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = "ACC-001" });
        var keys = new KeyAttributeCollection { { "accountnumber", "ACC-999" } };

        // Act & Assert
        await Assert.That(() => _sut.RetrieveWithAlternateKey("account", keys, new ColumnSet(true)))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task RetrieveWithAlternateKey_EmptyKeys_ThrowsObjectDoesNotExist()
    {
        // Arrange
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["name"] = "Only" });

        // Act & Assert
        await Assert.That(() => _sut.RetrieveWithAlternateKey("account", [], new ColumnSet(true)))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task RetrieveWithAlternateKey_NullKeyValue_DoesNotMatchStoredNull()
    {
        // Arrange
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = null });
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = null });
        var keys = new KeyAttributeCollection { { "accountnumber", null! } };

        // Act & Assert
        await Assert.That(() => _sut.RetrieveWithAlternateKey("account", keys, new ColumnSet(true)))
            .Throws<FaultException<OrganizationServiceFault>>();
    }

    [Test]
    public async Task RetrieveWithAlternateKey_RecordSeededWithKeyAttributesOnly_ReturnsRecord()
    {
        // Arrange
        var id = Guid.NewGuid();
        var seeded = new Entity("account", id)
        {
            ["name"] = "Seeded",
            KeyAttributes = { { "accountnumber", "ACC-001" } }
        };
        _sut.Add(seeded);

        // Act
        var result = _sut.RetrieveWithAlternateKey("account", new KeyAttributeCollection { { "accountnumber", "ACC-001" } }, new ColumnSet(true));

        // Assert
        await Assert.That(result.Id).IsEqualTo(id);
    }

    [Test]
    public async Task Add_EntityReferenceWithAlternateKey_ResolvesToExistingRecordId()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        _sut.Add(new Entity("account", accountId) { ["accountnumber"] = "acc-001" });

        var contact = new Entity("contact", Guid.NewGuid())
        {
            ["parentcustomerid"] = new EntityReference("account", "accountnumber", "ACC-001")
        };

        // Act
        _sut.Add(contact);

        // Assert
        var reference = contact.GetAttributeValue<EntityReference>("parentcustomerid");
        await Assert.That(reference.Id).IsEqualTo(accountId);
        await Assert.That(reference.KeyAttributes).Count().IsEqualTo(0);
    }
}
