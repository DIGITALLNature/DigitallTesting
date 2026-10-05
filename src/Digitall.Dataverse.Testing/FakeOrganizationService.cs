// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Reflection;
using System.ServiceModel;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.Extensions;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing;

public class FakeOrganizationService(TimeProvider timeProvider, FakeOrganizationServiceState state) : IOrganizationService
{
    public TimeProvider TimeProvider { get; } = timeProvider;
    public FakeDataverseOptions Options { get; } = new();

    public FakeOrganizationServiceState State { get; } = state;

    // ReSharper disable once UnusedMember.Global : Public API
    public FakeOrganizationService(FakeOrganizationServiceState state) : this(TimeProvider.System, state)
    {
    }

    public FakeOrganizationService(TimeProvider timeProvider) : this(timeProvider, new FakeOrganizationServiceState())
    {
    }

    public FakeOrganizationService() : this(TimeProvider.System)
    {
    }

    private Dictionary<string, Dictionary<Guid, Entity>> ServiceState => State.Entities;

    private EntityTypeResolver? _typeResolver;
    private EntityTypeResolver TypeResolver => _typeResolver ??= new EntityTypeResolver(State.ModelAssemblies, State.EntityMetadata);

    /// <summary>
    /// Invalidates the type resolver cache. Call after modifying ModelAssemblies or EntityMetadata.
    /// </summary>
    // ReSharper disable once UnusedMember.Global : Public API
    public void InvalidateTypeResolverCache()
    {
        _typeResolver = null;
    }

    private Dictionary<Type, IOrganizationRequestFake> OrganizationRequestFakes { get; } = new();

    /// <summary>
    /// Checks if a record exists in the internal state without throwing.
    /// </summary>
    internal bool EntityExists(string logicalName, Guid id) => ServiceState.TryGetValue(logicalName, out var entities) && entities.ContainsKey(id);

    /// <summary>
    ///     Runs <paramref name="action"/> and restores the record state if it throws.
    ///     Stored entities are replaced, never mutated in place, so a shallow snapshot is sufficient.
    /// </summary>
    internal T ExecuteAtomic<T>(Func<T> action)
    {
        var snapshot = ServiceState.ToDictionary(table => table.Key, table => new Dictionary<Guid, Entity>(table.Value));

        try
        {
            return action();
        }
        catch
        {
            ServiceState.Clear();
            foreach (var (logicalName, rows) in snapshot)
            {
                ServiceState[logicalName] = rows;
            }

            throw;
        }
    }

    public void AddRequest(IOrganizationRequestFake fake)
    {
        OrganizationRequestFakes.Add(fake.ForType, fake);
    }

    public void AddRequests(IEnumerable<IOrganizationRequestFake> requests)
    {
        foreach (var request in requests)
        {
            AddRequest(request);
        }
    }

    public void AddRequests(params IOrganizationRequestFake[] requests)
    {
        foreach (var request in requests)
        {
            AddRequest(request);
        }
    }

    public void AddMetadata(EntityMetadata entityMetadata)
    {
        State.EntityMetadata.Add(entityMetadata.LogicalName, entityMetadata);

        var relationships = new List<RelationshipMetadataBase>();
        if (entityMetadata.ManyToManyRelationships != null)
        {
            relationships.AddRange(entityMetadata.ManyToManyRelationships);
        }

        if (entityMetadata.OneToManyRelationships != null)
        {
            relationships.AddRange(entityMetadata.OneToManyRelationships);
        }

        if (entityMetadata.ManyToOneRelationships != null)
        {
            relationships.AddRange(entityMetadata.ManyToOneRelationships);
        }

        AddRelationships(relationships);
    }

    public void AddMetadata(IEnumerable<EntityMetadata> entityMetadata)
    {
        foreach (var metadata in entityMetadata)
        {
            AddMetadata(metadata);
        }
    }

    public void AddMetadata(params EntityMetadata[] entityMetadata)
    {
        foreach (var metadata in entityMetadata)
        {
            AddMetadata(metadata);
        }
    }

    public void AddRelationship(RelationshipMetadataBase relationship)
    {
        State.Relationships[relationship.SchemaName] = relationship;
    }

    public void AddRelationships(IEnumerable<RelationshipMetadataBase> relationships)
    {
        foreach (var relationship in relationships)
        {
            AddRelationship(relationship);
        }
    }

    public void AddRelationships(params RelationshipMetadataBase[] relationships)
    {
        foreach (var relationship in relationships)
        {
            AddRelationship(relationship);
        }
    }

    private static readonly Lazy<Dictionary<Type, IOrganizationRequestFake>> s_defaultFakes = new(() =>
    {
        var assembly = typeof(IOrganizationRequestFake).Assembly;
        var fakeTypes = assembly.GetTypes().Where(type =>
            type.IsClass && type is { IsAbstract: false, Namespace: "Digitall.Dataverse.Testing.OrganizationRequests" }
            && typeof(IOrganizationRequestFake).IsAssignableFrom(type));

        var result = new Dictionary<Type, IOrganizationRequestFake>();
        foreach (var type in fakeTypes)
        {
            if (Activator.CreateInstance(type) is IOrganizationRequestFake fake)
            {
                result.TryAdd(fake.ForType, fake);
            }
        }
        return result;
    });

    /// <summary>
    /// Registers all built-in request fakes into the user dictionary.
    /// No longer required — built-in fakes are automatically available as fallback.
    /// Kept for backward compatibility.
    /// </summary>
    public void AddDefaultRequests()
    {
        foreach (var (type, fake) in s_defaultFakes.Value)
        {
            if (!OrganizationRequestFakes.ContainsKey(type))
            {
                AddRequest(fake);
            }
        }
    }

    #region IQueryable

    public IQueryable<T> CreateQuery<T>() where T : Entity
    {
        var logicalName = TypeResolver.GetLogicalName(typeof(T))
                          ?? typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName
                          ?? throw new ArgumentException(
                              $"Entity type '{typeof(T).Name}' could not be resolved to a logical name. " +
                              "Ensure the type is annotated with [EntityLogicalName] and its assembly is registered " +
                              "as a ProxyTypesAssembly or added to ModelAssemblies.", nameof(T));

        return CreateQuery<T>(logicalName);
    }

    public IQueryable<T> CreateQuery<T>(string entityLogicalName) where T : Entity
    {
        var entityStateCopy = new List<T>();
        if (!ServiceState.TryGetValue(entityLogicalName, out var entityState))
        {
            return entityStateCopy.AsQueryable(); //Empty list
        }

        var primaryIdAttribute = GetPrimaryIdAttribute(entityLogicalName);

        entityStateCopy.AddRange(entityState.Values.Select(e =>
        {
            var clone = e.CloneEntity();
            EnsurePrimaryIdAttribute(clone, primaryIdAttribute);
            return typeof(T) == typeof(Entity) ? (T)clone : clone.ToEntity<T>();
        }));

        return entityStateCopy.AsQueryable();
    }

    /// <summary>
    ///     Determines the primary id attribute name for the given entity.
    ///     Uses <see cref="EntityMetadata.PrimaryIdAttribute"/> when available,
    ///     otherwise falls back to the Dataverse convention <c>&lt;entityLogicalName&gt;id</c>.
    /// </summary>
    private string GetPrimaryIdAttribute(string entityLogicalName)
    {
        if (State.EntityMetadata.TryGetValue(entityLogicalName, out var metadata) && !string.IsNullOrWhiteSpace(metadata.PrimaryIdAttribute))
        {
            return metadata.PrimaryIdAttribute!;
        }

        return entityLogicalName + "id";
    }

    /// <summary>
    ///     Ensures that the primary id attribute is present in the entity's attribute collection.
    ///     This allows filters (e.g. <c>systemuserid == &lt;guid&gt;</c>) to match against <see cref="Entity.Id"/>
    ///     even when the attribute was not explicitly set on the stored entity.
    /// </summary>
    private static void EnsurePrimaryIdAttribute(Entity entity, string primaryIdAttribute)
    {
        if (entity.Id == Guid.Empty) return;
        if (entity.Attributes.ContainsKey(primaryIdAttribute)) return;

        entity[primaryIdAttribute] = entity.Id;
    }

    public IQueryable<Entity> CreateQuery(string entityLogicalName) => CreateQuery<Entity>(entityLogicalName);

    #endregion

    public void Add(Entity entity)
    {
        if (!ServiceState.TryGetValue(entity.LogicalName, out var value))
        {
            value = new Dictionary<Guid, Entity>();
            ServiceState.Add(entity.LogicalName, value);
        }

        foreach (var entityRef in entity.Attributes.Values.OfType<EntityReference>().Where(er => er.KeyAttributes?.Count > 0))
        {
            var match = FindEntityByAlternateKey(entityRef.LogicalName, entityRef.KeyAttributes);

            if (match is null) continue;

            entityRef.KeyAttributes = [];
            entityRef.Id = match.Id;
        }

        value.Add(entity.Id, entity);
    }

    public void AddRange(IEnumerable<Entity> entities) => entities.ToList().ForEach(Add);

    internal RelationshipMetadataBase? GetRelationship(string relationshipSchemaName)
    {
        return State.Relationships.GetValueOrDefault(relationshipSchemaName);
    }

    /// <summary>
    ///     Checks if the specified entity type is known.
    ///     An entity type is considered known if it exists in the metadata or if it is an early bound type.
    /// </summary>
    /// <param name="logicalname">The logical name of the entity.</param>
    /// <param name="entityType">The Type of the entity if it is known, otherwise null.</param>
    /// <returns>True if the entity type is known, otherwise false.</returns>
    public bool EntityTypeIsKnown(string logicalname, out Type? entityType) => TypeResolver.EntityTypeIsKnown(logicalname, out entityType);

    /// <summary>
    ///     Checks if the specified attribute is known for the given entity.
    /// </summary>
    public bool IsKnownAttributeForType(string entity, string attribute, out PropertyInfo? attributeInfo) => TypeResolver.IsKnownAttributeForType(entity, attribute, out attributeInfo);

    /// <summary>
    ///     Throws an exception if the specified entity type is not known.
    /// </summary>
    public void ThrowIfNotKnownEntityType(string entityType) => TypeResolver.ThrowIfNotKnownEntityType(entityType);

    /// <summary>
    ///     Throws an exception if the specified attribute is not known for the given entity.
    /// </summary>
    public void ThrowIfNotKnownAttribute(string entityLogicalName, string attributeLogicalName) => TypeResolver.ThrowIfNotKnownAttribute(entityLogicalName, attributeLogicalName);

    /// <summary>
    ///     Converts a plain Entity to its registered proxy type using cached delegates.
    /// </summary>
    public Entity ConvertToProxyType(Entity entity) => TypeResolver.ConvertToProxyType(entity);

    #region IOrganizationService

    /// <summary>
    ///     Creates a new entity in the Dataverse. Routes through the Execute pipeline.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <returns>The Id of the created entity.</returns>
    /// <exception cref="FaultException">Thrown if the entity already exists in the Dataverse.</exception>
    public Guid Create(Entity? entity)
        => ((CreateResponse)Execute(new CreateRequest { Target = entity! })).id;

    /// <summary>
    ///     Internal create logic. Called by <see cref="OrganizationRequests.CreateFake"/> — not part of the pipeline.
    /// </summary>
    internal Guid CreateCore(Entity? entity)
    {
        if (entity == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Target' is missing");
        }

        var clone = entity.CloneEntity();
        if (clone.Id == Guid.Empty)
        {
            clone.Id = Guid.NewGuid();
        }

        var defaultStateCode = State.GetDefaultStateCode(clone.LogicalName);
        if (!clone.Contains("statecode")) clone["statecode"] = defaultStateCode;
        // Resolve statuscode based on the actual statecode (passed or defaulted)
        if (!clone.Contains("statuscode"))
        {
            var resolvedStatecode = clone.GetAttributeValue<OptionSetValue>("statecode");
            clone["statuscode"] = State.GetDefaultStatusCode(clone.LogicalName, resolvedStatecode.Value);
        }

        // Default ownerid for user-owned entities
        if (!clone.Contains("ownerid"))
        {
            var isOrganizationOwned = State.EntityMetadata.TryGetValue(clone.LogicalName, out var metadata)
                                      && metadata.OwnershipType == OwnershipTypes.OrganizationOwned;

            var entityTypeIsKnown = EntityTypeIsKnown(clone.LogicalName, out _);
            var ownerIdExistsOnType = IsKnownAttributeForType(clone.LogicalName, "ownerid", out _);

            if (!isOrganizationOwned && Options.UserId != Guid.Empty && (!entityTypeIsKnown || ownerIdExistsOnType))
            {
                clone["ownerid"] = new EntityReference("systemuser", Options.UserId);
            }
        }

        var now = TimeProvider.GetUtcNow().UtcDateTime;
        if (!clone.Contains("createdon"))  clone["createdon"]  = now;
        if (!clone.Contains("modifiedon")) clone["modifiedon"] = now;

        if (Options.UserId != Guid.Empty)
        {
            var userRef = new EntityReference("systemuser", Options.UserId);
            if (!clone.Contains("createdby"))  clone["createdby"]  = userRef;
            if (!clone.Contains("modifiedby")) clone["modifiedby"] = userRef;
        }

        clone.RowVersion ??= TimeProvider.GetUtcNow().Ticks.ToString();

        try
        {
            Add(clone);
        }
        catch (ArgumentException)
        {
            ErrorFactory.ThrowFault(ErrorCodes.DuplicateRecord, "Cannot insert duplicate key.");
        }

        return clone.Id;
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        => ((RetrieveResponse)Execute(new RetrieveRequest
        {
            Target = new EntityReference(entityName, id),
            ColumnSet = columnSet
        })).Entity;

    /// <summary>
    ///     Internal retrieve logic. Called by <see cref="OrganizationRequests.RetrieveFake"/> — not part of the pipeline.
    /// </summary>
    internal Entity RetrieveCore(string entityName, Guid id, ColumnSet columnSet)
    {
        if (!ServiceState.TryGetValue(entityName, out var value))
        {
            ThrowIfNotKnownEntityType(entityName);
        }

        Entity? record = null;
        if (value == null || !value.TryGetValue(id, out record))
        {
            ErrorFactory.ThrowFault(ErrorCodes.ObjectDoesNotExist, $"Entity '{entityName}' With Id = {id:D} Does Not Exist");
        }

        return record.ProjectAttributes(columnSet, this).CloneEntity();
    }

    public void Update(Entity? entity)
        => Execute(new UpdateRequest { Target = entity! });

    /// <summary>
    ///     Internal update logic. Called by <see cref="OrganizationRequests.UpdateFake"/> — not part of the pipeline.
    /// </summary>
    internal void UpdateCore(Entity? entity)
    {
        if (entity == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required field 'Target' is missing");
        }

        if (!ServiceState.TryGetValue(entity.LogicalName, out var value))
        {
            ThrowIfNotKnownEntityType(entity.LogicalName);
        }

        if (value == null || !value.TryGetValue(entity.Id, out _))
        {
            ErrorFactory.ThrowFault(ErrorCodes.ObjectDoesNotExist, $"Entity '{entity.LogicalName}' With Id = {entity.Id:D} Does Not Exist");
        }

        var merged = value[entity.Id].CloneEntity();

        foreach (var attr in entity.Attributes)
            merged.Attributes[attr.Key] = attr.Value;

        foreach (var fv in entity.FormattedValues)
            merged.FormattedValues[fv.Key] = fv.Value;

        foreach (var ka in entity.KeyAttributes)
            merged.KeyAttributes[ka.Key] = ka.Value;

        foreach (var re in entity.RelatedEntities)
            merged.RelatedEntities[re.Key] = re.Value;

        merged["modifiedon"] = TimeProvider.GetUtcNow().UtcDateTime;
        if (Options.UserId != Guid.Empty)
            merged["modifiedby"] = new EntityReference("systemuser", Options.UserId);
        merged.RowVersion = TimeProvider.GetUtcNow().Ticks.ToString();

        value[entity.Id] = merged;
    }

    public void Delete(string? entityName, Guid id)
    {
        if (entityName == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "Required member 'LogicalName' missing for field 'Target'");
        }

        Execute(new DeleteRequest { Target = new EntityReference(entityName, id) });
    }

    /// <summary>
    ///     Internal delete logic. Called by <see cref="OrganizationRequests.DeleteFake"/> — not part of the pipeline.
    /// </summary>
    internal void DeleteCore(string entityName, Guid id)
    {
        if (!ServiceState.TryGetValue(entityName, out var value))
        {
            ThrowIfNotKnownEntityType(entityName);
        }

        if (value == null || !value.TryGetValue(id, out _))
        {
            ErrorFactory.ThrowFault(ErrorCodes.ObjectDoesNotExist, $"Entity '{entityName}' With Id = {id:D} Does Not Exist");
        }

        value.Remove(id);
    }

    public OrganizationResponse Execute(OrganizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (OrganizationRequestFakes.TryGetValue(request.GetType(), out var fake))
            return fake.Execute(request, this);

        if (s_defaultFakes.Value.TryGetValue(request.GetType(), out var defaultFake))
            return defaultFake.Execute(request, this);

        ErrorFactory.ThrowFault(ErrorCodes.MessageDoesNotExist, $"No implementation found for request of type {request.GetType().Name}");
        return null!; // unreachable — ThrowFault is [DoesNotReturn]
    }

    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        => Execute(new AssociateRequest
        {
            Target = new EntityReference(entityName, entityId),
            Relationship = relationship,
            RelatedEntities = relatedEntities
        });


    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        => Execute(new DisassociateRequest
        {
            Target = new EntityReference(entityName, entityId),
            Relationship = relationship,
            RelatedEntities = relatedEntities
        });

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        return ((RetrieveMultipleResponse)Execute(new RetrieveMultipleRequest { Query = query })).EntityCollection;
    }

    #endregion

    public Entity RetrieveWithAlternateKey(string entityName, KeyAttributeCollection keys, ColumnSet columnSet)
    {
        if (!ServiceState.TryGetValue(entityName, out _))
        {
            ThrowIfNotKnownEntityType(entityName);
        }

        var record = FindEntityByAlternateKey(entityName, keys);
        if (record == null)
        {
            ErrorFactory.ThrowFault(ErrorCodes.ObjectDoesNotExist, $"Entity '{entityName}' With Key = {string.Join(",", keys.Keys)} Does Not Exist");
        }

        return record.ProjectAttributes(columnSet, this).CloneEntity();
    }

    internal Entity? FindEntityByAlternateKey(string entityName, KeyAttributeCollection keys)
    {
        if (keys.Count == 0 || !ServiceState.TryGetValue(entityName, out var value))
        {
            return null;
        }

        return value.Values.SingleOrDefault(row => keys.All(key =>
            TryGetAttributeOrKeyValue(row, key.Key, out var val) && KeyValueEquals(val, key.Value)));
    }

    private static bool TryGetAttributeOrKeyValue(Entity entity, string keyName, out object? value)
    {
        if (entity.Attributes.TryGetValue(keyName, out value))
        {
            return true;
        }

        if (entity.KeyAttributes.ContainsKey(keyName))
        {
            value = entity.KeyAttributes[keyName];
            return true;
        }

        value = null;
        return false;
    }

    private static bool KeyValueEquals(object? storedValue, object? keyValue)
    {
        if (storedValue is null || keyValue is null) return false;
        if (storedValue is string s1 && keyValue is string s2)
            return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
        if (storedValue is OptionSetValue osv1 && keyValue is OptionSetValue osv2)
            return osv1.Value == osv2.Value;
        if (storedValue is OptionSetValue osv && keyValue is int intVal)
            return osv.Value == intVal;
        if (storedValue is int intVal2 && keyValue is OptionSetValue osv3)
            return intVal2 == osv3.Value;
        return Equals(storedValue, keyValue);
    }
}
