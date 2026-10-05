# Digitall.Dataverse.Testing

[![NuGet](https://img.shields.io/nuget/v/Digitall.Dataverse.Testing)](https://www.nuget.org/packages/Digitall.Dataverse.Testing)
[![Build](https://github.com/DIGITALLNature/DigitallTesting/actions/workflows/build.yml/badge.svg)](https://github.com/DIGITALLNature/DigitallTesting/actions/workflows/build.yml)
[![License: MS-RL](https://img.shields.io/badge/License-MS--RL-blue.svg)](LICENSE.md)

A comprehensive, in-memory testing framework for **Microsoft Dataverse** / Power Platform. It provides a lightweight `IOrganizationService` (and `IOrganizationServiceAsync2`) implementation that enables fast, deterministic unit tests **without any connection to a live Dataverse environment**.

---

## Table of Contents

- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Core Components](#core-components)
  - [FakeOrganizationService](#fakeorganizationservice)
  - [FakeOrganizationServiceAsync](#fakeorganizationserviceasync)
  - [FakeDataverseBuilder](#fakedataversebuilder)
  - [PluginExecutionContextBuilder](#pluginexecutioncontextbuilder)
  - [FakePluginContextBuilder](#fakeplugincontextbuilder)
  - [SpyOrganizationRequestFake](#spyorganizationrequestfake)
- [Query Support](#query-support)
  - [QueryExpression](#queryexpression)
  - [QueryByAttribute](#querybyattribute)
  - [FetchXml](#fetchxml)
  - [FetchXml Aggregation](#fetchxml-aggregation)
  - [LINQ Queries](#linq-queries)
- [Organization Request Fakes](#organization-request-fakes)
- [Relationship Management](#relationship-management)
- [Metadata Support](#metadata-support)
- [Plugin Testing](#plugin-testing)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Build & Test](#build--test)
- [CI/CD & Release](#cicd--release)
- [Contributing](#contributing)
- [License](#license)

---

## Features

| Category | Capability |
|----------|-----------|
| **CRUD Operations** | Full Create, Retrieve, Update, Delete with real Dataverse error codes |
| **Async Support** | `IOrganizationServiceAsync2` implementation with `CancellationToken` support |
| **Query Engine** | `QueryExpression`, `QueryByAttribute`, `FetchXml` (including aggregation) |
| **LINQ** | `CreateQuery<T>()` support for early-bound and late-bound LINQ queries |
| **Relationships** | 1:N, N:1, and N:N relationship association/disassociation |
| **Plugin Testing** | Fluent `PluginExecutionContextBuilder` for IPluginExecutionContext7 |
| **Early-Bound** | Auto-discovery of `[ProxyTypesAssembly]` types with reflection caching |
| **Request Extensibility** | Pluggable `IOrganizationRequestFake` architecture |
| **Spy Support** | `SpyOrganizationRequestFake<TReq, TRes>` for recording and verifying calls |
| **Time Abstraction** | `TimeProvider` injection for deterministic time-based tests |
| **Error Fidelity** | Authentic `FaultException<OrganizationServiceFault>` with real error codes |
| **Performance** | FastCloner deep cloning, cached reflection lookups |

---

## Prerequisites

- **.NET SDK 10.0+** (see `global.json` for exact version)
- **IDE**: JetBrains Rider, Visual Studio 2022+, or VS Code with C# Dev Kit

---

## Installation

Install from NuGet:

```bash
dotnet add package Digitall.Dataverse.Testing
```

Or add to your `.csproj`:

```xml
<PackageReference Include="Digitall.Dataverse.Testing" Version="1.0.0-beta.*" />
```

---

## Quick Start

### Basic CRUD Test

```csharp
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Digitall.Dataverse.Testing;

public class AccountTests
{
    [Test]
    public async Task Should_CreateAndRetrieveEntity()
    {
        // Arrange — use the builder for a fully configured service
        var service = new FakeDataverseBuilder().GetOrganizationService();

        var account = new Entity("account") { ["name"] = "Contoso Ltd" };

        // Act
        var id = service.Create(account);
        var retrieved = service.Retrieve("account", id, new ColumnSet("name"));

        // Assert
        await Assert.That(retrieved["name"]).IsEqualTo("Contoso Ltd");
    }
}
```

### Using Early-Bound Entities

Ensure your test assembly has the `ProxyTypesAssembly` attribute:

```csharp
[assembly: Microsoft.Xrm.Sdk.Client.ProxyTypesAssembly]
```

Then use typed entities directly:

```csharp
var service = new FakeDataverseBuilder().GetOrganizationService();
var account = new Account { Name = "Contoso Ltd" };
var id = service.Create(account);
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Your Test Code                           │
├─────────────────────────────────────────────────────────────┤
│  FakeDataverseBuilder / FakePluginContextBuilder (Fluent)    │
├─────────────────────────────────────────────────────────────┤
│  FakeOrganizationServiceAsync (IOrganizationServiceAsync2)   │
│  └── FakeOrganizationService (IOrganizationService)          │
│       ├── RequestFakeRegistry → IOrganizationRequestFake     │
│       ├── EntityTypeResolver (reflection + caching)          │
│       ├── FakeOrganizationServiceState (entity store)        │
│       └── MetadataService (entity/relationship metadata)     │
├─────────────────────────────────────────────────────────────┤
│  Logic Layer                                                 │
│  ├── QueryProcessor (orchestrates query execution)           │
│  ├── ExpressionProcessor (filter/condition evaluation)       │
│  ├── LinkedEntitiesProcessor (JOIN logic)                    │
│  ├── FetchProcessor (FetchXml → QueryExpression)             │
│  ├── ConditionParser (50+ ConditionOperator types)           │
│  └── FetchAggregation (COUNT, SUM, AVG, MIN, MAX, grouping) │
└─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### FakeOrganizationService

The central class implementing `IOrganizationService`. It provides:

- **CRUD**: `Create`, `Retrieve`, `Update`, `Delete` with proper error handling
- **Queries**: `RetrieveMultiple` supporting all three query types
- **Relationships**: `Associate` / `Disassociate` for 1:N and N:N
- **Execute**: Dispatches `OrganizationRequest` to registered `IOrganizationRequestFake` handlers
- **Alternate Keys**: `Retrieve` with `KeyAttributeCollection` support

```csharp
var service = new FakeOrganizationService();
service.AddDefaultRequests(); // registers built-in request fakes
```

### FakeOrganizationServiceAsync

Extends `FakeOrganizationService` with `IOrganizationServiceAsync2`, wrapping all operations in `Task`-returning methods with `CancellationToken` support. Ideal for testing code that uses `IOrganizationServiceAsync` or `ServiceClient`.

```csharp
var service = new FakeOrganizationServiceAsync();
var id = await service.CreateAsync(entity, cancellationToken);
```

### FakeDataverseBuilder

A fluent builder that creates a fully configured `FakeOrganizationServiceAsync` with default request fakes pre-registered:

```csharp
var service = new FakeDataverseBuilder()
    .AddData(account, contact)              // pre-seed entities
    .AddEntityMetadata(accountMetadata)     // register metadata
    .AddRelationships(m2mRelationship)      // register relationships
    .LoadMetadata("./metadata/")            // load from XML files
    .GetOrganizationService();
```

**Builder extension methods:**
- `.AddData(params Entity[])` — pre-seed the in-memory store
- `.AddOrganizationRequests(params IOrganizationRequestFake[])` — register custom fakes
- `.AddEntityMetadata(params EntityMetadata[])` — register entity metadata
- `.AddRelationships(params RelationshipMetadataBase[])` — register relationships
- `.LoadMetadata(string path)` — load `EntityMetadata` from XML (file or directory)
- `.AddConfig(key, defaultValue, value?)` — add Dataverse environment variable definition/value entities
- `.WithUserId(Guid)` — set the current user ID
- `.WithBusinessUnitId(Guid)` — set the current business unit ID
- `.WithMaxRetrieveCount(int)` — set the max records per page
- `.WithFiscalYearStart(DateOnly)` — set the fiscal year start date

### PluginExecutionContextBuilder

Fluent builder for creating `IServiceProvider` instances configured for plugin testing:

```csharp
var serviceProvider = new PluginExecutionContextBuilder(organizationService)
    .WithMessageName("Update")
    .WithStage(40)                          // Post-operation
    .WithMode(0)                            // Synchronous
    .WithTarget(targetEntity)
    .WithPreEntityImage(preImage)
    .WithPostEntityImage(postImage)
    .WithInputParameter("Target", entity)
    .WithOutputParameter("id", resultId)
    .WithSharedVariable("key", "value")
    .WithInitiatingUserId(userId)
    .WithDepth(1)
    .WithCorrelationId(correlationId)
    .BuildServiceProvider();

// Resolve plugin services
var context = (IPluginExecutionContext7)serviceProvider.GetService(typeof(IPluginExecutionContext7));
var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
```

### FakePluginContextBuilder

Combines `PluginExecutionContextBuilder` with an auto-configured `FakeOrganizationService`, implementing `IFakeDataverseBuilder<FakeOrganizationService>`. Perfect for end-to-end plugin tests:

```csharp
var builder = new FakePluginContextBuilder();

// Access the underlying service for data setup
var service = builder.GetOrganizationService();
service.Create(new Entity("account") { ["name"] = "Test" });

// Build the plugin context
var serviceProvider = builder
    .WithMessageName("Create")
    .WithTarget(entity)
    .BuildServiceProvider();
```

### SpyOrganizationRequestFake

A generic spy that records all `Execute` calls and allows configuring return values:

```csharp
// Create a spy for a custom request
var spy = new SpyOrganizationRequestFake<MyCustomRequest, MyCustomResponse>(
    (request, service) => new MyCustomResponse { Result = "OK" }
);

service.AddRequest(spy);

// Execute the request
service.Execute(new MyCustomRequest());

// Verify
await Assert.That(spy.ReceivedRequests).Count().IsEqualTo(1);
```

---

## Query Support

### QueryExpression

Full support for `QueryExpression` including:

- **ColumnSet** projection (specific columns or `AllColumns`)
- **FilterExpression** with nested `And`/`Or` logical operators
- **50+ ConditionOperators** (Equal, NotEqual, Like, In, Between, Null, fiscal year operators, etc.)
- **LinkEntity** joins (Inner, LeftOuter, Natural, MatchFirstRowUsingCrossApply, Any, NotAny, All, NotAll, Exists, In) with nested link entities
- **FilterExpression.AnyAllFilterLinkEntity** for EXISTS/NOT EXISTS subqueries at the filter level
- **ConditionExpression.CompareColumns** for column-to-column comparison within the same row
- **OrderExpression** (ascending/descending on multiple attributes), including orders defined on linked entities (`LinkEntity.Orders`, applied after the root entity's orders and recursively across nested links)
- **Paging** via `PageInfo` with proper `MoreRecords` and `PagingCookie` support
- **TopCount** limiting
- **Distinct** result filtering

```csharp
var query = new QueryExpression("contact")
{
    ColumnSet = new ColumnSet("firstname", "lastname"),
    Criteria = new FilterExpression(LogicalOperator.And)
    {
        Conditions =
        {
            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
            new ConditionExpression("lastname", ConditionOperator.Like, "Smith%")
        }
    },
    LinkEntities =
    {
        new LinkEntity("contact", "account", "parentcustomerid", "accountid", JoinOperator.Inner)
        {
            EntityAlias = "acc",
            Columns = new ColumnSet("name")
        }
    },
    Orders = { new OrderExpression("lastname", OrderType.Ascending) },
    PageInfo = new PagingInfo { Count = 50, PageNumber = 1, ReturnTotalRecordCount = true }
};

var result = service.RetrieveMultiple(query);
```

### QueryByAttribute

Simplified attribute-based queries (internally converted to `QueryExpression`):

```csharp
var query = new QueryByAttribute("account")
{
    ColumnSet = new ColumnSet("name", "revenue")
};
query.AddAttributeValue("statecode", 0);
query.AddAttributeValue("ownerid", userId);

var result = service.RetrieveMultiple(query);
```

### FetchXml

Full FetchXml parsing with conversion to `QueryExpression`:

```csharp
var fetchXml = @"
<fetch top='10'>
  <entity name='account'>
    <attribute name='name' />
    <attribute name='revenue' />
    <filter>
      <condition attribute='statecode' operator='eq' value='0' />
    </filter>
    <order attribute='name' />
  </entity>
</fetch>";

var result = service.RetrieveMultiple(new FetchExpression(fetchXml));
```

### FetchXml Aggregation

Supports aggregate queries with grouping:

```csharp
var fetchXml = @"
<fetch aggregate='true'>
  <entity name='opportunity'>
    <attribute name='estimatedvalue' alias='total_value' aggregate='sum' />
    <attribute name='ownerid' alias='owner' groupby='true' />
    <filter>
      <condition attribute='statecode' operator='eq' value='0' />
    </filter>
  </entity>
</fetch>";

var result = service.RetrieveMultiple(new FetchExpression(fetchXml));
```

**Supported aggregates:** `count`, `countcolumn`, `sum`, `avg`, `min`, `max`
**Grouping:** By attribute value, by date (day, week, month, quarter, year, fiscal period/year)

### LINQ Queries

```csharp
// Early-bound
var accounts = service.CreateQuery<Account>()
    .Where(a => a.Name.StartsWith("Contoso"))
    .ToList();

// Late-bound
var contacts = service.CreateQuery("contact")
    .Where(c => (string)c["lastname"] == "Smith")
    .ToList();
```

---

## Organization Request Fakes

Built-in fakes for common Dataverse operations:

| Request Type | Fake Class | Description |
|-------------|-----------|-------------|
| `CreateRequest` | `CreateFake` | Entity creation with duplicate detection and deep insert |
| `CreateMultipleRequest` | `CreateMultipleFake` | Bulk entity creation with deep insert |
| `RetrieveRequest` | `RetrieveFake` | Entity retrieval with column projection |
| `RetrieveMultipleRequest` | `RetrieveMultipleFake` | Query execution pipeline |
| `UpdateRequest` | `UpdateFake` | Entity updates with existence validation |
| `UpdateMultipleRequest` | `UpdateMultipleFake` | Bulk entity updates with existence validation |
| `DeleteRequest` | `DeleteFake` | Entity deletion |
| `UpsertRequest` | `UpsertFake` | Create-or-update semantics with alternate keys and deep insert |
| `AssociateRequest` | `AssociateFake` | Relationship association |
| `DisassociateRequest` | `DisassociateFake` | Relationship disassociation |
| `SetStateRequest` | `SetStateFake` | Entity state/status changes |
| `AssignRequest` | `AssignRequestFake` | Record ownership assignment |
| `WhoAmIRequest` | `WhoAmIFake` | Current user identity |
| `RetrieveEntityRequest` | `RetrieveEntityFake` | Entity metadata retrieval |
| `RetrieveAllEntitiesRequest` | `RetrieveAllEntitiesFake` | Retrieve metadata for all known entities |
| `RetrieveAttributeRequest` | `RetrieveAttributeFake` | Attribute metadata retrieval |
| `RetrieveRelationshipRequest` | `RetrieveRelationshipFake` | Relationship metadata retrieval by name or metadata ID |
| `QueryExpressionToFetchXmlRequest` | `QueryExpressionToFetchXmlFake` | Convert a `QueryExpression` to FetchXml |
| `FetchXmlToQueryExpressionRequest` | `FetchXmlToQueryExpressionFake` | Convert FetchXml to a `QueryExpression` |
| `ExecuteTransactionRequest` | `ExecuteTransactionFake` | Batch transaction execution |
| `ExecuteMultipleRequest` | `ExecuteMultipleFake` | Batch execution with error handling and response collection |
| `BulkDeleteRequest` | `BulkDeleteFake` | Bulk delete operations |

### Custom Request Fakes

Implement `IOrganizationRequestFake` or extend `OrganizationRequestFake<TReq, TRes>`:

```csharp
public class MyCustomRequestFake : OrganizationRequestFake<MyCustomRequest, MyCustomResponse>
{
    public override MyCustomResponse Execute(MyCustomRequest request, FakeOrganizationService service)
    {
        // Custom logic
        return new MyCustomResponse { /* ... */ };
    }
}

// Register
service.AddRequest(new MyCustomRequestFake());
```

---

## Relationship Management

### 1:N Relationships

```csharp
// Register relationship metadata
service.AddRelationship(new OneToManyRelationshipMetadata
{
    SchemaName = "account_contacts",
    ReferencedEntity = "account",
    ReferencedAttribute = "accountid",
    ReferencingEntity = "contact",
    ReferencingAttribute = "parentcustomerid"
});

// Associate contacts to an account
service.Associate("account", accountId,
    new Relationship("account_contacts"),
    new EntityReferenceCollection
    {
        new EntityReference("contact", contactId1),
        new EntityReference("contact", contactId2)
    });
```

### N:N Relationships

```csharp
// Register M2M relationship
service.AddRelationship(new ManyToManyRelationshipMetadata
{
    SchemaName = "systemuser_account",
    Entity1LogicalName = "systemuser",
    Entity1IntersectAttribute = "systemuserid",
    Entity2LogicalName = "account",
    Entity2IntersectAttribute = "accountid",
    IntersectEntityName = "systemuser_account"
});

// Associate
service.Associate("account", accountId,
    new Relationship("systemuser_account"),
    new EntityReferenceCollection { new EntityReference("systemuser", userId) });

// Disassociate
service.Disassociate("account", accountId,
    new Relationship("systemuser_account"),
    new EntityReferenceCollection { new EntityReference("systemuser", userId) });
```

### Deep Insert (Create with RelatedEntities)

`CreateFake` and `UpsertFake` support deep insert — creating child entities via `Entity.RelatedEntities` in a single operation. Relationship metadata must be registered beforehand.

```csharp
// Register the relationship
service.State.Relationships["contact_customer_accounts"] = new OneToManyRelationshipMetadata
{
    SchemaName = "contact_customer_accounts",
    ReferencedEntity = "account",
    ReferencedAttribute = "accountid",
    ReferencingEntity = "contact",
    ReferencingAttribute = "parentcustomerid"
};

// Deep insert: create account with related contacts
var account = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Contoso" };
var contact = new Entity("contact") { ["lastname"] = "Smith" };

account.RelatedEntities[new Relationship("contact_customer_accounts")] =
    new EntityCollection([contact]);

service.Create(account);
// → account created, contact created with parentcustomerid = account.Id
```

Deep insert works recursively (children can have their own `RelatedEntities`) and supports both 1:N and N:N relationships. Sub-entities are always created, never updated — matching Dataverse behavior.

---

## Metadata Support

Entity metadata enables type validation and relationship processing:

```csharp
// Programmatic metadata registration
service.AddMetadata(new EntityMetadata
{
    LogicalName = "account",
    // ... attributes, relationships
});

// Load from serialized XML files (DataContractSerializer format)
var service = new FakeDataverseBuilder()
    .LoadMetadata("./metadata/account.xml")   // single file
    .LoadMetadata("./metadata/")              // all *.xml in directory
    .GetOrganizationService();
```

---

## Plugin Testing

### End-to-End Plugin Test

```csharp
[Test]
public async Task MyPlugin_OnAccountUpdate_ShouldSetModifiedFlag()
{
    // Arrange
    var builder = new FakePluginContextBuilder();
    var service = builder.GetOrganizationService();

    var account = new Entity("account") { Id = Guid.NewGuid(), ["name"] = "Old Name" };
    service.Create(account);

    var target = new Entity("account") { Id = account.Id, ["name"] = "New Name" };

    var serviceProvider = builder
        .WithMessageName("Update")
        .WithStage(40)
        .WithTarget(target)
        .WithPreEntityImage(account, "PreImage")
        .BuildServiceProvider();

    // Act
    var plugin = new MyPlugin();
    plugin.Execute(serviceProvider);

    // Assert
    var updated = service.Retrieve("account", account.Id, new ColumnSet(true));
    await Assert.That((bool)updated["modifiedflag"]).IsTrue();
}
```

### Testing with TimeProvider

```csharp
using Microsoft.Extensions.Time.Testing;

var fakeTime = new FakeTimeProvider(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero));
var service = new FakeOrganizationService(fakeTime);

// Advance time in tests
fakeTime.Advance(TimeSpan.FromDays(30));
```

---

## Configuration

The `FakeOrganizationService` exposes a `FakeDataverseOptions` instance via the `Options` property for per-test configuration. This avoids process-global state (such as environment variables) and is safe for parallel test execution.

| Option | Type | Description | Default |
|--------|------|-------------|---------|
| `UserId` | `Guid` | Current user ID (used in `WhoAmI`, `EqualUserId` filters, and default record ownership) | `Guid.Empty` |
| `BusinessUnitId` | `Guid` | Current business unit ID (used in `WhoAmI` and `EqualBusinessId` filters) | `Guid.Empty` |
| `FiscalYearStart` | `DateOnly?` | Start date for fiscal year calculations | `null` (defaults to Jan 1) |
| `MaxRetrieveCount` | `int` | Maximum records returned by `RetrieveMultiple` per page | `5000` |

Configure via the builder:

```csharp
var service = new FakeDataverseBuilder()
    .WithUserId(userId)
    .WithBusinessUnitId(businessUnitId)
    .WithMaxRetrieveCount(100)
    .WithFiscalYearStart(new DateOnly(2025, 4, 1))
    .GetOrganizationService();
```

Or set directly on the service:

```csharp
var service = new FakeOrganizationService();
service.Options.UserId = userId;
service.Options.MaxRetrieveCount = 100;
```

---

## Project Structure

```
DigitallTesting/
├── src/Digitall.Dataverse.Testing/             # Core library (NuGet package)
│   ├── FakeOrganizationService.cs              # IOrganizationService implementation
│   ├── FakeOrganizationServiceAsync.cs         # IOrganizationServiceAsync2 implementation
│   ├── FakeOrganizationServiceState.cs         # Internal entity store
│   ├── FakeDataverseOptions.cs                 # Per-instance configuration options
│   ├── FakeDataverseBuilder.cs                 # Fluent builder for service setup
│   ├── FakePluginContextBuilder.cs             # Combined plugin + service builder
│   ├── IFakeDataverseBuilder.cs                # Builder interface
│   ├── PluginExecutionContextBuilder.cs        # Plugin context configuration
│   ├── EntityTypeResolver.cs                   # Early-bound type discovery & caching
│   ├── SpyOrganizationRequestFake.cs           # Generic spy for request verification
│   ├── Extensions/                             # Extension methods
│   │   ├── EntityExtensions.cs                 # Entity cloning, projection, joins
│   │   ├── FakeDataverseBuilderExtensions.cs   # Builder fluent API
│   │   ├── FakeOrganizationServiceStateExtensions.cs # State helper extensions
│   │   ├── PluginExecutionContextBuilderExtensions.cs
│   │   ├── QueryExpressionExtensions.cs        # Query helpers
│   │   ├── DateTimeExtensions.cs               # Fiscal year/date utilities
│   │   ├── DeepCloneExtensions.cs              # FastCloner integration
│   │   ├── TypeExtensions.cs                   # Reflection helpers
│   │   └── XDocumentExtensions.cs              # FetchXml parsing
│   ├── Logic/                                  # Query processing engine
│   │   ├── Queries/
│   │   │   ├── QueryProcessor.cs               # Query orchestration
│   │   │   ├── ExpressionProcessor.cs          # Filter evaluation
│   │   │   ├── ConditionParser.cs              # 50+ condition operators
│   │   │   ├── LinkedEntitiesProcessor.cs      # JOIN logic
│   │   │   ├── FetchProcessor.cs               # FetchXml → QueryExpression
│   │   │   ├── QueryExpressionExtensions.cs    # Query-specific extensions
│   │   │   ├── TypedConditionExpression.cs     # Typed condition wrapper
│   │   │   ├── Validators.cs                   # Type/attribute validation
│   │   │   └── FetchAggregation/               # Aggregate functions
│   │   │       ├── CountAggregate.cs
│   │   │       ├── CountColumnAggregate.cs
│   │   │       ├── CountDistinctAggregate.cs
│   │   │       ├── SumAggregate.cs
│   │   │       ├── AvgAggregate.cs
│   │   │       ├── MinAggregate.cs
│   │   │       ├── MaxAggregate.cs
│   │   │       └── ...                         # Grouping support
│   │   └── XrmOrderByAttributeComparer.cs      # Sorting logic
│   ├── OrganizationRequests/                   # Built-in request fakes
│   │   ├── IOrganizationRequestFake.cs         # Extension interface
│   │   ├── OrganizationRequestFake.cs          # Typed base class
│   │   ├── CreateFake.cs
│   │   ├── DeepInsertProcessor.cs              # Deep insert helper
│   │   ├── RetrieveFake.cs
│   │   ├── RetrieveMultipleFake.cs
│   │   ├── UpdateFake.cs
│   │   ├── DeleteFake.cs
│   │   ├── UpsertFake.cs
│   │   ├── AssociateFake.cs
│   │   ├── DisassociateFake.cs
│   │   ├── SetStateFake.cs
│   │   ├── AssignRequestFake.cs
│   │   ├── WhoAmIFake.cs
│   │   ├── RetrieveEntityFake.cs
│   │   ├── RetrieveAllEntitiesFake.cs
│   │   ├── QueryExpressionToFetchXmlFake.cs
│   │   ├── FetchXmlToQueryExpressionFake.cs
│   │   ├── ExecuteTransactionFake.cs
│   │   └── BulkDeleteFake.cs
│   ├── Model/                                  # Internal models
│   │   └── Target.cs                           # Plugin target wrapper
│   └── Errors/                                 # Error infrastructure
│       ├── ErrorCodes.cs                       # Dataverse error code constants
│       └── ErrorFactory.cs                     # FaultException factory
├── tests/Digitall.Dataverse.Testing.Tests/     # Unit tests
│   ├── FakeOrganizationServiceTests.cs
│   ├── FakeDataverseBuilderTests.cs
│   ├── FakePluginContextBuilderTests.cs
│   ├── PluginExecutionContextBuilderTests.cs
│   ├── Extensions/                             # Extension method tests
│   ├── Logic/                                  # Query processor tests
│   ├── OrganizationRequests/                   # Request fake tests
│   └── Fixtures/                              # Test data and helpers
├── .github/workflows/                          # CI/CD
│   ├── build.yml                               # PR build + test
│   ├── release.yml                             # Semantic release + NuGet publish
│   ├── checks.yml                              # Additional checks
│   ├── codeql.yml                              # Security scanning
│   └── qodana_code_quality.yml                 # JetBrains Qodana analysis
├── Directory.Build.props                       # Shared MSBuild properties & versioning
├── global.json                                 # .NET SDK version pinning
├── package.json                                # semantic-release & commitlint config
└── qodana.yaml                                 # Qodana configuration
```

---

## Build & Test

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run all tests
dotnet test --configuration Release

# Run specific tests by filter (TUnit uses --treenode-filter)
dotnet test --treenode-filter "/*/*/*/QueryProcessor*"

# Run tests with detailed output
dotnet test -- --output Detailed
```

---

## CI/CD & Release

This project uses [semantic-release](https://github.com/semantic-release/semantic-release) for automated versioning and publishing:

- **Build workflow** (`build.yml`): Runs on all non-release branches; builds and tests the solution.
- **Release workflow** (`release.yml`): Runs on `main` and `beta` branches and can also be started manually via `workflow_dispatch`; performs semantic versioning, NuGet publish, changelog generation, and GitHub release creation.
- **Release checkout requirements**: `release.yml` uses full git history (`fetch-depth: 0`) and fetches tags to ensure semantic-release can resolve previous versions correctly.
- **Commit conventions**: [Conventional Commits](https://www.conventionalcommits.org/) enforced via [commitlint](https://commitlint.js.org/) and [Husky](https://typicode.github.io/husky/) git hooks.

### Commit Message Format

```
type(scope): description

feat: add FetchXml aggregate support       → minor version bump
fix: correct paging cookie generation      → patch version bump
feat!: remove deprecated API               → major version bump
```

---

## Contributing

1. Fork the repository
2. Create a feature branch (`feat/my-feature`)
3. Write tests for your changes
4. Ensure all tests pass (`dotnet test`)
5. Commit using [Conventional Commits](https://www.conventionalcommits.org/)
6. Open a Pull Request

---

## License

This project is licensed under the **Microsoft Reciprocal License (MS-RL)**. See [LICENSE.md](LICENSE.md) for details.

© 2024 DIGITALL Nature GmbH
