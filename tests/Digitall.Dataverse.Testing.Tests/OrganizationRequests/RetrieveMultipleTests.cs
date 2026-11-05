// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.Tests.Fixtures;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.Tests.OrganizationRequests;

public class RetrieveMultipleTests
{
    [Test]
    public async Task Stubs_Dispatch_Working()
    {
        var sut = new FakeOrganizationService();

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName));

        await Assert.That(result).IsNotNull();
    }

    #region QueryExpression

    [Test]
    public async Task QueryExpression_Top()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }

    [Test]
    public async Task QueryExpression_Paging()
    {
        var sut = new FakeOrganizationService{ Options = { MaxRetrieveCount = 10 }};

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 19).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(10);
        await Assert.That(result.PagingCookie).IsNotNull();
        await Assert.That(result.MoreRecords).IsTrue();

        result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),PageInfo = new PagingInfo
        {
            PagingCookie = result.PagingCookie,
            PageNumber = 2
        }

        });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(9);
        await Assert.That(result.PagingCookie).IsNull();
        await Assert.That(result.MoreRecords).IsFalse();
    }

    [Test]
    public async Task QueryExpression_EmptyPageOnPaging()
    {
        var sut = new FakeOrganizationService{ Options = { MaxRetrieveCount = 10 }};

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(10);
        await Assert.That(result.PagingCookie).IsNotNull();
        await Assert.That(result.MoreRecords).IsTrue();

        result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),PageInfo = new PagingInfo
            {
                PagingCookie = result.PagingCookie,
                PageNumber = 21 // out of range
            }

        });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).IsEmpty();
        await Assert.That(result.PagingCookie).IsNull();
        await Assert.That(result.MoreRecords).IsFalse();

    }

    [Test]
    [Arguments(0, 2, 1, 0, false)]
    [Arguments(0, 2, 3, 0, false)]
    [Arguments(3, 2, 1, 2, true)]
    [Arguments(3, 2, 2, 1, false)]
    [Arguments(4, 2, 2, 2, false)]
    [Arguments(3, 2, 3, 0, false)]
    [Arguments(3, 2, 100, 0, false)]
    [Arguments(3, 2, int.MaxValue, 0, false)]
    [Arguments(3, int.MaxValue, 2, 0, false)]
    [Arguments(3, int.MaxValue, int.MaxValue, 0, false)]
    [Arguments(3, 0, 3, 0, false)]
    public async Task QueryExpression_PagingBoundaries_ReturnsExpectedPage(
        int recordCount, int pageSize, int pageNumber, int expectedCount, bool moreRecords)
    {
        var sut = new FakeOrganizationServiceAsync { Options = { MaxRetrieveCount = 2 } };
        sut.AddRange(Enumerable.Range(0, recordCount)
            .Select(index => new Account(Guid.NewGuid()) { Name = $"Account {index}" }));
        var query = new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(Account.LogicalNames.Name),
            Orders = { new OrderExpression(Account.LogicalNames.Name, OrderType.Ascending) },
            PageInfo = new PagingInfo
            {
                Count = pageSize,
                PageNumber = pageNumber,
                ReturnTotalRecordCount = true
            }
        };

        var result = await sut.RetrieveMultipleAsync(query, CancellationToken.None);

        await Assert.That(result.Entities).Count().IsEqualTo(expectedCount);
        await Assert.That(result.MoreRecords).IsEqualTo(moreRecords);
        await Assert.That(result.TotalRecordCount).IsEqualTo(recordCount);
        await Assert.That(result.EntityName).IsEqualTo(Account.EntityLogicalName);
        if (moreRecords)
        {
            await Assert.That(result.PagingCookie).IsNotNull();
        }
        else
        {
            await Assert.That(result.PagingCookie).IsNull();
        }

        var effectivePageSize = pageSize == 0 ? 2 : pageSize;
        var expectedNames = Enumerable.Range(0, recordCount)
            .Skip((int)Math.Min((long)(pageNumber - 1) * effectivePageSize, recordCount))
            .Take(expectedCount)
            .Select(index => $"Account {index}");
        await Assert.That(result.Entities.Select(entity => entity.GetAttributeValue<string>(Account.LogicalNames.Name)))
            .IsEquivalentTo(expectedNames);
    }

    [Test]
    [Arguments(-1, 2)]
    [Arguments(0, 0)]
    [Arguments(0, -1)]
    public async Task QueryExpression_InvalidPageSize_ThrowsFault(int pageSize, int maxRetrieveCount)
    {
        var sut = new FakeOrganizationService { Options = { MaxRetrieveCount = maxRetrieveCount } };
        var query = new QueryExpression(Account.EntityLogicalName)
        {
            PageInfo = new PagingInfo { Count = pageSize, PageNumber = 1 }
        };

        var exception = Assert.Throws<FaultException<OrganizationServiceFault>>(() => sut.RetrieveMultiple(query));

        await Assert.That(exception.Detail.ErrorCode).IsEqualTo((int)ErrorCodes.InvalidArgument);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetrieveMultiple_OtherQueryTypesBeyondLastPage_ReturnsEmptyPage(bool useFetchXml)
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(Enumerable.Range(0, 3)
            .Select(index => new Account(Guid.NewGuid()) { Name = $"Account {index}" }));
        QueryBase query = useFetchXml
            ? new FetchExpression("<fetch count='2' page='3'><entity name='account'><attribute name='name'/></entity></fetch>")
            : new QueryByAttribute(Account.EntityLogicalName)
            {
                ColumnSet = new ColumnSet(Account.LogicalNames.Name),
                PageInfo = new PagingInfo { Count = 2, PageNumber = 3 }
            };

        var result = sut.RetrieveMultiple(query);

        await Assert.That(result.Entities).IsEmpty();
        await Assert.That(result.MoreRecords).IsFalse();
        await Assert.That(result.PagingCookie).IsNull();
    }

    [Test]
    public async Task QueryExpression_Destinct()
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(TestData.Default);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) {
            ColumnSet = new ColumnSet(Account.LogicalNames.Name),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Account.LogicalNames.Name, ConditionOperator.Equal, "B Corp")
                }
            },
            LinkEntities = { new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName, Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId, JoinOperator.Inner) }}
        );

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(2);

        var resultDestinct = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) {
            ColumnSet = new ColumnSet(Account.LogicalNames.Name),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Account.LogicalNames.Name, ConditionOperator.Equal, "B Corp")
                }
            },
            LinkEntities = { new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName, Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId, JoinOperator.Inner) },
            Distinct = true
            }
        );

        await Assert.That(resultDestinct).IsNotNull();
        await Assert.That(resultDestinct.Entities).Count().IsEqualTo(1);
    }

    [Test]
    public async Task QueryExpression_Empty()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }


    [Test]
    public async Task QueryExpression_TotalRecords()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }

    [Test]
    public async Task QueryExpression_Order()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 50).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}", ExchangeRate = x}));

        sut.AddRange(manyRecords);

        var resultDescending = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),
            Orders = { new OrderExpression(Account.LogicalNames.ExchangeRate, OrderType.Descending) } });
        await Assert.That(resultDescending).IsNotNull();
        var descItems = resultDescending.Entities.Select(a => a.ToEntity<Account>()).ToList();
        await Assert.That(descItems.SequenceEqual(descItems.OrderByDescending(x => x.ExchangeRate))).IsTrue();

        var resultAscending = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),
            Orders = { new OrderExpression(Account.LogicalNames.ExchangeRate, OrderType.Ascending) } });
        await Assert.That(resultAscending).IsNotNull();
        var ascItems = resultAscending.Entities.Select(a => a.ToEntity<Account>()).ToList();
        await Assert.That(ascItems.SequenceEqual(ascItems.OrderBy(x => x.ExchangeRate))).IsTrue();
    }

    #endregion

    #region QueryByAttribute
   [Test]
    public async Task QueryByAttribute_Top()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }

    [Test]
    public async Task QueryByAttribute_Paging()
    {
        var sut = new FakeOrganizationService{  Options = { MaxRetrieveCount = 10 }};

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 19).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(10);
        await Assert.That(result.PagingCookie).IsNotNull();
        await Assert.That(result.MoreRecords).IsTrue();

        result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),PageInfo = new PagingInfo
        {
            PagingCookie = result.PagingCookie,
            PageNumber = 2
        }

        });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(9);
        await Assert.That(result.PagingCookie).IsNull();
        await Assert.That(result.MoreRecords).IsFalse();
    }

    [Test]
    public async Task QueryByAttribute_EmptyPageOnPaging()
    {
        var sut = new FakeOrganizationService { Options = { MaxRetrieveCount = 10 } };

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(10);
        await Assert.That(result.PagingCookie).IsNotNull();
        await Assert.That(result.MoreRecords).IsTrue();

        result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),PageInfo = new PagingInfo
            {
                PagingCookie = result.PagingCookie,
                PageNumber = 21 // out of range
            }

        });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).IsEmpty();
        await Assert.That(result.PagingCookie).IsNull();
        await Assert.That(result.MoreRecords).IsFalse();

    }

    [Test]
    public async Task QueryByAttribute_Empty()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }


    [Test]
    public async Task QueryByAttribute_TotalRecords()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 200).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}"}));

        sut.AddRange(manyRecords);

        var result = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { TopCount = 5, ColumnSet = new ColumnSet(true) });
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(5);
    }

    [Test]
    public async Task QueryByAttribute_Order()
    {
        var sut = new FakeOrganizationService();

        var manyRecords = new List<Account>();
        Enumerable.Range(0, 50).ToList().ForEach(x => manyRecords.Add(new Account(Guid.NewGuid()){Name = $"Account {x}", ExchangeRate = x}));

        sut.AddRange(manyRecords);

        var resultDescending = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),
            Orders = { new OrderExpression(Account.LogicalNames.ExchangeRate, OrderType.Descending) } });
        await Assert.That(resultDescending).IsNotNull();
        var descItems = resultDescending.Entities.Select(a => a.ToEntity<Account>()).ToList();
        await Assert.That(descItems.SequenceEqual(descItems.OrderByDescending(x => x.ExchangeRate))).IsTrue();

        var resultAscending = sut.RetrieveMultiple(new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true),
            Orders = { new OrderExpression(Account.LogicalNames.ExchangeRate, OrderType.Ascending) } });
        await Assert.That(resultAscending).IsNotNull();
        var ascItems = resultAscending.Entities.Select(a => a.ToEntity<Account>()).ToList();
        await Assert.That(ascItems.SequenceEqual(ascItems.OrderBy(x => x.ExchangeRate))).IsTrue();
    }
    #endregion

    #region LeftOuter Join

    /// <summary>
    /// Regression test: a LeftOuter join must return the outer (left-side) entity even when no
    /// matching inner entity exists. The TestData contains <c>corpA</c> which has no contacts
    /// linked via <c>parentcustomerid</c>; it must appear in the result without any aliased
    /// contact attributes.
    /// </summary>
    [Test]
    public async Task QueryExpression_LeftOuterJoin_OuterEntityReturnedWhenNoRelatedEntityExists()
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(TestData.Default);

        // corpA has no contacts; corpB has two contacts
        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            LinkEntities =
            {
                new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName,
                    Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId,
                    JoinOperator.LeftOuter)
                {
                    EntityAlias = "c",
                    Columns = new ColumnSet(Contact.LogicalNames.FirstName)
                }
            }
        });

        await Assert.That(result).IsNotNull();

        var corpARows = result.Entities
            .Where(e => e.Id == Guid.Parse("00000000-0000-0000-0001-000000000001"))
            .ToList();

        // corpA must appear at least once despite having no linked contact
        await Assert.That(corpARows).IsNotEmpty();

        // The unmatched row must not carry aliased contact attributes
        var unmatchedRow = corpARows.Single();
        await Assert.That(unmatchedRow.Attributes.ContainsKey("c." + Contact.LogicalNames.FirstName)).IsFalse();
    }

    /// <summary>
    /// Regression test: a LeftOuter join must produce one result row per matching inner entity.
    /// <c>corpB</c> has two contacts so it must appear twice in the result set.
    /// </summary>
    [Test]
    public async Task QueryExpression_LeftOuterJoin_MultipleRelatedEntitiesProduceMultipleRows()
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(TestData.Default);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            LinkEntities =
            {
                new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName,
                    Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId,
                    JoinOperator.LeftOuter)
                {
                    EntityAlias = "c",
                    Columns = new ColumnSet(Contact.LogicalNames.FirstName)
                }
            }
        });

        await Assert.That(result).IsNotNull();

        var corpBRows = result.Entities
            .Where(e => e.Id == Guid.Parse("00000000-0000-0000-0001-000000000002"))
            .ToList();

        // corpB must appear once for each of its two contacts
        await Assert.That(corpBRows).Count().IsEqualTo(2);

        // Each row must be an independent object instance (not the same mutated reference)
        await Assert.That(ReferenceEquals(corpBRows[0], corpBRows[1])).IsFalse();

        // Each row must carry the aliased first name of its specific contact
        var firstNames = corpBRows
            .Select(e => (e.GetAttributeValue<AliasedValue>("c." + Contact.LogicalNames.FirstName)?.Value as string)!)
            .OrderBy(n => n)
            .ToList();
        await Assert.That(firstNames).IsEquivalentTo(["John B", "John C"]);
    }

    /// <summary>
    /// Regression test: a LeftOuter join must return more rows than the equivalent Inner join
    /// because unmatched outer rows are preserved. With TestData.Default:
    /// <list type="bullet">
    ///   <item>Inner join: 2 rows (corpB × conB, corpB × conC)</item>
    ///   <item>LeftOuter join: 3 rows (corpA × null, corpB × conB, corpB × conC)</item>
    /// </list>
    /// </summary>
    [Test]
    public async Task QueryExpression_LeftOuterJoin_ReturnsMoreRowsThanInnerJoin()
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(TestData.Default);

        var linkEntity = new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName,
            Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId,
            JoinOperator.Inner);

        var innerResult = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            LinkEntities = { linkEntity }
        });

        linkEntity.JoinOperator = JoinOperator.LeftOuter;

        var outerResult = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            LinkEntities = { linkEntity }
        });

        await Assert.That(innerResult.Entities).Count().IsEqualTo(2);
        await Assert.That(outerResult.Entities).Count().IsEqualTo(3);
    }

    /// <summary>
    /// Regression test: each row produced by a LeftOuter join must be an independent entity
    /// instance. When the outer entity matches multiple inner entities the aliased attributes
    /// of every result row must reflect the specific inner entity for that row, not the last
    /// one written (i.e. no shared-mutation side-effect).
    /// </summary>
    [Test]
    public async Task QueryExpression_LeftOuterJoin_EachRowHasIndependentAliasedValues()
    {
        var sut = new FakeOrganizationService();
        sut.AddRange(TestData.Default);

        // corpB matches conB (FirstName "John B") and conC (FirstName "John C")
        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            LinkEntities =
            {
                new LinkEntity(Account.EntityLogicalName, Contact.EntityLogicalName,
                    Account.LogicalNames.AccountId, Contact.LogicalNames.ParentCustomerId,
                    JoinOperator.LeftOuter)
                {
                    EntityAlias = "c",
                    Columns = new ColumnSet(Contact.LogicalNames.FirstName)
                }
            }
        });

        var corpBRows = result.Entities
            .Where(e => e.Id == Guid.Parse("00000000-0000-0000-0001-000000000002"))
            .ToList();

        await Assert.That(corpBRows).Count().IsEqualTo(2);

        var firstNames = corpBRows
            .Select(e => (e.GetAttributeValue<AliasedValue>("c." + Contact.LogicalNames.FirstName)?.Value as string)!)
            .OrderBy(n => n)
            .ToList();

        // Both contacts must be present with their own distinct first name
        await Assert.That(firstNames).IsEquivalentTo(["John B", "John C"]);
    }

    #endregion

    #region PrimaryId filter

    /// <summary>
    /// Regression test: filtering by the primary key attribute (e.g. <c>accountid</c>) must match
    /// against <see cref="Microsoft.Xrm.Sdk.Entity.Id"/>, even when the attribute was not explicitly
    /// set on the stored entity. This is what the SDK LINQ provider produces for <c>e.Id == guid</c>.
    /// </summary>
    [Test]
    public async Task QueryExpression_FilterByPrimaryIdAttribute_FindsEntity()
    {
        var sut = new FakeOrganizationService();

        var targetId = Guid.NewGuid();
        sut.AddRange([
            new Account(Guid.NewGuid()) { Name = "Other A" },
            new Account(targetId)       { Name = "Target" },
            new Account(Guid.NewGuid()) { Name = "Other B" }
        ]);

        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName)
        {
            ColumnSet = new ColumnSet(true),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression(Account.LogicalNames.AccountId, ConditionOperator.Equal, targetId)
                }
            }
        });

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(1);
        await Assert.That(result.Entities[0].Id).IsEqualTo(targetId);
    }

    [Test]
    public async Task QueryByAttribute_FilterByPrimaryIdAttribute_FindsEntity()
    {
        var sut = new FakeOrganizationService();

        var targetId = Guid.NewGuid();
        sut.AddRange([
            new Account(Guid.NewGuid()) { Name = "Other A" },
            new Account(targetId)       { Name = "Target" },
            new Account(Guid.NewGuid()) { Name = "Other B" }
        ]);

        var query = new QueryByAttribute(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) };
        query.AddAttributeValue(Account.LogicalNames.AccountId, targetId);

        var result = sut.RetrieveMultiple(query);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Entities).Count().IsEqualTo(1);
        await Assert.That(result.Entities[0].Id).IsEqualTo(targetId);
    }

    #endregion

    #region ConvertToProxyType

    /// <summary>
    /// Verifies that <c>RetrieveMultiple</c> converts stored base <see cref="Entity"/> instances
    /// to their early-bound proxy types. The entity is deliberately added as a plain (upcasted)
    /// <see cref="Entity"/> — confirmed via <c>Retrieve</c>, which returns the raw stored type —
    /// while <c>RetrieveMultiple</c> must expose the concrete proxy type <see cref="Account"/>.
    /// </summary>
    [Test]
    public async Task RetrieveMultiple_ReturnsProxyTypedEntities_WhenStoredAsBaseEntity()
    {
        var sut = new FakeOrganizationService();
        var id = Guid.NewGuid();

        // Deliberately store a plain Entity (not Account) to force the ConvertToProxyType path
        sut.Add(new Entity(Account.EntityLogicalName, id) { [Account.LogicalNames.Name] = "Proxy Test" });

        // Retrieve bypasses ConvertToProxyType — confirms state holds a plain Entity
        var stored = sut.Retrieve(Account.EntityLogicalName, id, new ColumnSet(true));
        await Assert.That(stored.GetType()).IsEqualTo(typeof(Entity));

        // RetrieveMultiple must convert it to the proxy type
        var result = sut.RetrieveMultiple(new QueryExpression(Account.EntityLogicalName) { ColumnSet = new ColumnSet(true) });

        await Assert.That(result.Entities).Count().IsEqualTo(1);
        await Assert.That(result.Entities[0]).IsTypeOf<Account>();
        await Assert.That(result.Entities[0].Id).IsEqualTo(id);
    }

    #endregion
}
