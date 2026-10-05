// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Runtime.Serialization;
using Digitall.Dataverse.Testing.OrganizationRequests;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;

namespace Digitall.Dataverse.Testing.Extensions;

public static class FakeDataverseBuilderExtensions
{
    /// <param name="builder">builder instance</param>
    extension<TBuilder>(TBuilder builder) where TBuilder : IFakeDataverseBuilder<FakeOrganizationService>
    {
        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder AddData(IEnumerable<Entity> records)
        {
            builder.GetOrganizationService().AddRange(records);
            return builder;
        }

        public TBuilder AddData(params Entity[] records)
        {
            builder.GetOrganizationService().AddRange(records);
            return builder;
        }

        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder AddOrganizationRequests(IEnumerable<IOrganizationRequestFake> requests)
        {
            builder.GetOrganizationService().AddRequests(requests);
            return builder;
        }

        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder AddOrganizationRequests(params IOrganizationRequestFake[] requests)
        {
            builder.GetOrganizationService().AddRequests(requests);
            return builder;
        }

        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder AddEntityMetadata(IEnumerable<EntityMetadata> metadata)
        {
            builder.GetOrganizationService().AddMetadata(metadata);
            return builder;
        }

        public TBuilder AddEntityMetadata(params EntityMetadata[] metadata)
        {
            builder.GetOrganizationService().AddMetadata(metadata);
            return builder;
        }

        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder AddRelationships(IEnumerable<RelationshipMetadataBase> relationships)
        {
            builder.GetOrganizationService().AddRelationships(relationships);
            return builder;
        }

        public TBuilder AddRelationships(params RelationshipMetadataBase[] relationships)
        {
            builder.GetOrganizationService().AddRelationships(relationships);
            return builder;
        }

        public TBuilder WithUserId(Guid userId)
        {
            var service = builder.GetOrganizationService();
            service.Options.UserId = userId;
            return builder;
        }

        public TBuilder WithBusinessUnitId(Guid businessUnitId)
        {
            var service = builder.GetOrganizationService();
            service.Options.BusinessUnitId = businessUnitId;
            return builder;
        }

        public TBuilder WithFiscalYearStart(DateOnly fiscalYearStart)
        {
            var service = builder.GetOrganizationService();
            service.Options.FiscalYearStart = fiscalYearStart;
            return builder;
        }

        public TBuilder WithMaxRetrieveCount(int maxRetrieveCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetrieveCount);
            var service = builder.GetOrganizationService();
            service.Options.MaxRetrieveCount = maxRetrieveCount;
            return builder;
        }

        public TBuilder WithOrganizationVersion(string version)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
            var service = builder.GetOrganizationService();
            service.Options.OrganizationVersion = version;
            return builder;
        }

        public TBuilder WithOrganizationId(Guid organizationId)
        {
            var service = builder.GetOrganizationService();
            service.Options.OrganizationId = organizationId;
            return builder;
        }

        public TBuilder WithOrganizationName(string uniqueName, string? friendlyName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uniqueName);
            var service = builder.GetOrganizationService();
            service.Options.OrganizationUniqueName = uniqueName;
            if (friendlyName != null)
            {
                service.Options.OrganizationFriendlyName = friendlyName;
            }
            return builder;
        }

        /// <summary>
        /// Adds an environment variable configuration entry to the builder's organization service.
        /// Optionally adds a value override on top of the default.
        /// </summary>
        public TBuilder AddConfig(string key, string defaultValue, string? value = null)
        {
            var envVarDef = new Entity("environmentvariabledefinition") { Id = Guid.NewGuid(), ["schemaname"] = key, ["defaultvalue"] = defaultValue };
            builder.GetOrganizationService().Add(envVarDef);

            if (string.IsNullOrWhiteSpace(value)) return builder;

            var envVarVal = new Entity("environmentvariablevalue") { Id = Guid.NewGuid(), ["environmentvariabledefinitionid"] = envVarDef.ToEntityReference(), ["value"] = value };
            builder.GetOrganizationService().Add(envVarVal);

            return builder;
        }

        /// <summary>
        /// Loads EntityMetadata from XML files or a directory.
        /// </summary>
        /// <param name="path">The path to load from. Can be a file path to the XML file or path to the directory containing the XML files.</param>
        /// <returns>builder instance</returns>
        /// <exception cref="InvalidOperationException">path parameter is not a valid path</exception>
        // ReSharper disable once UnusedMember.Global : Public API
        public TBuilder LoadMetadata(string path)
        {
            var serializer = new DataContractSerializer(typeof(EntityMetadata));

            if (File.Exists(path))
            {
                using var fileStream = File.OpenRead(path);
                var metadata = (EntityMetadata?)serializer.ReadObject(fileStream);
                if (metadata != null) builder.GetOrganizationService().AddMetadata(metadata);
            }
            else if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.xml"))
                {
                    using var fileStream = File.OpenRead(file);
                    var metadata = (EntityMetadata?)serializer.ReadObject(fileStream);
                    if (metadata != null) builder.GetOrganizationService().AddMetadata(metadata);
                }
            }
            else
            {
                throw new InvalidOperationException($"'{path}' is not a valid path.");
            }

            return builder;
        }
    }
}
