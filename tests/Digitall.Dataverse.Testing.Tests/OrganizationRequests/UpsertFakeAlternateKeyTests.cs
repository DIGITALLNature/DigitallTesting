// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class UpsertFakeAlternateKeyTests
{
    private FakeOrganizationService _sut = null!;

    [Before(Test)]
    public async Task Setup()
    {
        _sut = new FakeOrganizationService
        {
            Options = { UserId = Guid.NewGuid() }
        };

        // Register OneToMany relationship: account -> contacts for deep insert tests
        _sut.State.Relationships["contact_customer_accounts"] = new OneToManyRelationshipMetadata
        {
            SchemaName = "contact_customer_accounts",
            ReferencedEntity = "account",
            ReferencedAttribute = "accountid",
            ReferencingEntity = "contact",
            ReferencingAttribute = "parentcustomerid"
        };

        await Task.CompletedTask;
    }

    [Test]
    public async Task Upsert_SingleColumnAlternateKey_ExistingRecord_UpdatesWithoutDuplicate()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account", existingId)
        {
            ["accountnumber"] = "ACC-001",
            ["name"] = "Before"
        });

        var target = new Entity("account")
        {
            ["name"] = "After",
            KeyAttributes = { { "accountnumber", "ACC-001" } }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)response.Results["Target"]).Id).IsEqualTo(existingId);
        await Assert.That(target.Id).IsEqualTo(Guid.Empty);

        var accounts = _sut.CreateQuery("account").ToList();
        await Assert.That(accounts).Count().IsEqualTo(1);

        var retrieved = _sut.Retrieve("account", existingId, new ColumnSet(true));
        await Assert.That(retrieved["name"]).IsEqualTo("After");
        await Assert.That(retrieved["accountnumber"]).IsEqualTo("ACC-001");
    }

    [Test]
    public async Task Upsert_CompositeAlternateKey_ExistingRecord_UpdatesMatchingRecord()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        _sut.Add(new Entity("account", id1)
        {
            ["accountnumber"] = "ACC-001",
            ["address1_city"] = "Berlin",
            ["name"] = "Berlin Branch"
        });
        _sut.Add(new Entity("account", id2)
        {
            ["accountnumber"] = "ACC-001",
            ["address1_city"] = "London",
            ["name"] = "London Branch"
        });

        var target = new Entity("account")
        {
            ["name"] = "Berlin Updated",
            KeyAttributes =
            {
                { "accountnumber", "ACC-001" },
                { "address1_city", "Berlin" }
            }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)response.Results["Target"]).Id).IsEqualTo(id1);

        var account1 = _sut.Retrieve("account", id1, new ColumnSet(true));
        await Assert.That(account1["name"]).IsEqualTo("Berlin Updated");

        var account2 = _sut.Retrieve("account", id2, new ColumnSet(true));
        await Assert.That(account2["name"]).IsEqualTo("London Branch");
    }

    [Test]
    public async Task Upsert_AlternateKey_NonExistingRecord_CreatesRecordAndPersistsKeyValues()
    {
        // Arrange
        var target = new Entity("account")
        {
            ["name"] = "New Account",
            KeyAttributes = { { "accountnumber", "ACC-NEW" } }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsTrue();
        var createdRef = (EntityReference)response.Results["Target"];
        await Assert.That(createdRef.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(target.Id).IsEqualTo(Guid.Empty);
        await Assert.That(target.Attributes.ContainsKey("accountnumber")).IsFalse();

        var retrieved = _sut.Retrieve("account", createdRef.Id, new ColumnSet(true));
        await Assert.That(retrieved["name"]).IsEqualTo("New Account");
        await Assert.That(retrieved["accountnumber"]).IsEqualTo("ACC-NEW");
    }

    [Test]
    public async Task Upsert_AlternateKey_RepeatedRequest_UpdatesSameRecordWithoutDuplicate()
    {
        // Arrange - first upsert (creates)
        var target1 = new Entity("account")
        {
            ["name"] = "Initial",
            KeyAttributes = { { "accountnumber", "ACC-REP" } }
        };

        var response1 = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target1 });
        await Assert.That((bool)response1.Results["RecordCreated"]).IsTrue();
        var initialId = ((EntityReference)response1.Results["Target"]).Id;

        // Act - second upsert with same key (updates)
        var target2 = new Entity("account")
        {
            ["name"] = "Repeated",
            KeyAttributes = { { "accountnumber", "ACC-REP" } }
        };

        var response2 = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target2 });

        // Assert
        await Assert.That((bool)response2.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)response2.Results["Target"]).Id).IsEqualTo(initialId);

        var accounts = _sut.CreateQuery("account").ToList();
        await Assert.That(accounts).Count().IsEqualTo(1);

        var retrieved = _sut.Retrieve("account", initialId, new ColumnSet(true));
        await Assert.That(retrieved["name"]).IsEqualTo("Repeated");
        await Assert.That(retrieved["accountnumber"]).IsEqualTo("ACC-REP");
    }

    [Test]
    public async Task Upsert_AlternateKey_ExistingRecord_DoesNotOverwriteKeyValueFromAttributes()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account", existingId)
        {
            ["accountnumber"] = "ACC-001",
            ["name"] = "Before"
        });

        var target = new Entity("account")
        {
            ["accountnumber"] = "ACC-CHANGED",
            ["name"] = "After",
            KeyAttributes = { { "accountnumber", "ACC-001" } }
        };

        // Act
        _sut.Execute(new UpsertRequest { Target = target });

        // Assert
        var retrieved = _sut.Retrieve("account", existingId, new ColumnSet(true));
        await Assert.That(retrieved["accountnumber"]).IsEqualTo("ACC-001");
        await Assert.That(retrieved["name"]).IsEqualTo("After");
    }

    [Test]
    public async Task Upsert_AlternateKey_NonExistingRecord_UsesKeyValueFromAttributesWhenPresent()
    {
        // Arrange
        var target = new Entity("account")
        {
            ["accountnumber"] = "ACC-ATTR",
            KeyAttributes = { { "accountnumber", "ACC-KEY" } }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        var id = ((EntityReference)response.Results["Target"]).Id;
        var retrieved = _sut.Retrieve("account", id, new ColumnSet(true));
        await Assert.That(retrieved["accountnumber"]).IsEqualTo("ACC-ATTR");
    }

    [Test]
    public async Task Upsert_IdTakesPrecedenceOverAlternateKey()
    {
        // Arrange
        var keyMatchId = Guid.NewGuid();
        var idMatchId = Guid.NewGuid();
        _sut.Add(new Entity("account", keyMatchId) { ["accountnumber"] = "ACC-001", ["name"] = "ByKey" });
        _sut.Add(new Entity("account", idMatchId) { ["accountnumber"] = "ACC-002", ["name"] = "ById" });

        var target = new Entity("account", idMatchId)
        {
            ["name"] = "Updated",
            KeyAttributes = { { "accountnumber", "ACC-001" } }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That(((EntityReference)response.Results["Target"]).Id).IsEqualTo(idMatchId);
        await Assert.That(_sut.Retrieve("account", idMatchId, new ColumnSet(true))["name"]).IsEqualTo("Updated");
        await Assert.That(_sut.Retrieve("account", keyMatchId, new ColumnSet(true))["name"]).IsEqualTo("ByKey");
    }

    [Test]
    public async Task Upsert_AlternateKey_NullKeyValue_DoesNotMatchStoredNull()
    {
        // Arrange
        _sut.Add(new Entity("account", Guid.NewGuid()) { ["accountnumber"] = null, ["name"] = "Existing" });

        var target = new Entity("account");
        target.KeyAttributes["accountnumber"] = null!;
        target["name"] = "New";

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsTrue();
        await Assert.That(_sut.CreateQuery("account").ToList()).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Upsert_IdBased_ExistingAndNew_RemainsUnchanged()
    {
        // Arrange - ID-based create
        var newId = Guid.NewGuid();
        var newAccount = new Entity("account", newId) { ["name"] = "CreatedById" };

        var createResponse = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = newAccount });
        await Assert.That((bool)createResponse.Results["RecordCreated"]).IsTrue();
        await Assert.That(((EntityReference)createResponse.Results["Target"]).Id).IsEqualTo(newId);

        // Act - ID-based update
        var updateAccount = new Entity("account", newId) { ["name"] = "UpdatedById" };
        var updateResponse = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = updateAccount });

        // Assert
        await Assert.That((bool)updateResponse.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)updateResponse.Results["Target"]).Id).IsEqualTo(newId);

        var retrieved = _sut.Retrieve("account", newId, new ColumnSet(true));
        await Assert.That(retrieved["name"]).IsEqualTo("UpdatedById");
    }

    [Test]
    public async Task Upsert_AlternateKey_CaseInsensitiveString_MatchesExistingRecord()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account", existingId)
        {
            ["accountnumber"] = "acc-001",
            ["name"] = "Before"
        });

        var target = new Entity("account")
        {
            ["name"] = "After",
            KeyAttributes = { { "accountnumber", "ACC-001" } }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)response.Results["Target"]).Id).IsEqualTo(existingId);

        var retrieved = _sut.Retrieve("account", existingId, new ColumnSet(true));
        await Assert.That(retrieved["name"]).IsEqualTo("After");
    }

    [Test]
    public async Task Upsert_AlternateKey_UpdatePath_WithRelatedEntities_DeepInsertsChildren()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        _sut.Add(new Entity("account", existingId)
        {
            ["accountnumber"] = "ACC-DEEP",
            ["name"] = "Parent"
        });

        var contact = new Entity("contact") { ["lastname"] = "Child" };
        var target = new Entity("account")
        {
            ["name"] = "Parent Updated",
            KeyAttributes = { { "accountnumber", "ACC-DEEP" } },
            RelatedEntities =
            {
                [new Relationship("contact_customer_accounts")] = new EntityCollection([contact])
            }
        };

        // Act
        var response = (UpsertResponse)_sut.Execute(new UpsertRequest { Target = target });

        // Assert
        await Assert.That((bool)response.Results["RecordCreated"]).IsFalse();
        await Assert.That(((EntityReference)response.Results["Target"]).Id).IsEqualTo(existingId);

        var contacts = _sut.CreateQuery("contact").ToList();
        await Assert.That(contacts).Count().IsEqualTo(1);
        await Assert.That(contacts[0].GetAttributeValue<EntityReference>("parentcustomerid").Id).IsEqualTo(existingId);
    }
}
