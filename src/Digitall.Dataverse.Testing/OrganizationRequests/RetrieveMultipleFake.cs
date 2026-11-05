// Copyright (c) DIGITALL Nature. All rights reserved
// DIGITALL Nature licenses this file to you under the Microsoft Public License.

using System.Xml.Linq;
using Digitall.Dataverse.Testing.Errors;
using Digitall.Dataverse.Testing.Extensions;
using Digitall.Dataverse.Testing.Logic.Queries;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Digitall.Dataverse.Testing.OrganizationRequests;

public class RetrieveMultipleFake : OrganizationRequestFake<RetrieveMultipleRequest, RetrieveMultipleResponse>
{
    public override RetrieveMultipleResponse Execute(RetrieveMultipleRequest organizationRequest, FakeOrganizationService fakeOrganizationService)
    {
        ArgumentNullException.ThrowIfNull(organizationRequest);

        // Create a new QueryProcessor with the given state
        var queryProcessor = new QueryProcessor(fakeOrganizationService);

        // Initialize variables
        QueryExpression queryExpression;
        string? entityName = null;
        List<Entity> internalResult;

        switch (organizationRequest.Query)
        {
            // Check if the query is a QueryExpression
            case QueryExpression expression:
                {
                    // Clone the QueryExpression
                    queryExpression = expression.CloneQuery();
                    entityName = queryExpression.EntityName;

                    // Execute the query and store the results in a list
                    var linqQuery = queryProcessor.ExecuteQueryExpression(queryExpression);
                    internalResult = linqQuery.ToList();
                    break;
                }
            case QueryByAttribute query:
                {
                    // We instantiate a QueryExpression to be executed as we have the implementation done already
                    queryExpression = new QueryExpression(query.EntityName);
                    entityName = query.EntityName;

                    queryExpression.ColumnSet = query.ColumnSet;
                    queryExpression.Criteria = new FilterExpression();
                    for (var i = 0; i < query.Attributes.Count; i++)
                    {
                        queryExpression.Criteria.AddCondition(new ConditionExpression(query.Attributes[i], ConditionOperator.Equal, query.Values[i]));
                    }

                    foreach (var order in query.Orders)
                    {
                        queryExpression.AddOrder(order.AttributeName, order.OrderType);
                    }

                    queryExpression.PageInfo = query.PageInfo;
                    queryExpression.TopCount = query.TopCount;

                    // QueryExpression now done... execute it!
                    var linqQuery = queryProcessor.ExecuteQueryExpression(queryExpression);
                    internalResult = linqQuery.ToList();
                    break;
                }
            case FetchExpression fetchXml:
                {
                    var xmlDoc = ParseXml(fetchXml.Query);
                    queryExpression = queryProcessor.ConvertXmlDocumentToQueryExpression(xmlDoc);
                    entityName = queryExpression.EntityName;

                    // QueryExpression now done... execute it!
                    var linqQuery = queryProcessor.ExecuteQueryExpression(queryExpression);
                    internalResult = linqQuery.ToList();

                    if (xmlDoc.IsAggregateFetchXml())
                    {
                        internalResult = QueryProcessor.ProcessAggregateFetchXml(xmlDoc, internalResult);
                    }

                    break;
                }
            default:
                ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, $"Query type '{organizationRequest.Query.GetType().Name}' is not supported");
                queryExpression = null!; // unreachable
                internalResult = null!;  // unreachable
                break;
        }


        // If the query has distinct set to true, remove duplicates from the list
        if (queryExpression.Distinct)
        {
            internalResult = GetDistinctEntities(internalResult);
        }

        // If the query has a TopCount set, limit the number of results returned
        if (queryExpression.TopCount != null && queryExpression.TopCount.Value < internalResult.Count)
        {
            internalResult = internalResult.Take(queryExpression.TopCount.Value).ToList();
        }

        // Handle TotalRecordCount here
        var totalRecordCount = -1;
        if (queryExpression is { PageInfo.ReturnTotalRecordCount: true })
        {
            totalRecordCount = internalResult.Count;
        }

        // Handle paging
        var maxRetrieveCount = fakeOrganizationService.Options.MaxRetrieveCount;
        var pageSize = maxRetrieveCount;
        var pageInfo = queryExpression.PageInfo;
        var pageNumber = 1;

        // Calculate the start position and number of items to retrieve based on the page number and page size
        if (pageInfo is { PageNumber: > 0 })
        {
            pageNumber = pageInfo.PageNumber;
            pageSize = pageInfo.Count == 0 ? maxRetrieveCount : pageInfo.Count;
        }

        if (pageSize <= 0)
        {
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, "The paging count must be positive.");
        }

        var startPosition = (long)(pageNumber - 1) * pageSize;
        var numberToGet = (int)Math.Min(pageSize, Math.Max(0L, internalResult.Count - startPosition));
        var recordsToReturn = numberToGet == 0 ? [] : internalResult.GetRange((int)startPosition, numberToGet);

        recordsToReturn.ForEach(e => PatchDateFormat(e, fakeOrganizationService));
        recordsToReturn.ForEach(FillFormattedValues);

        recordsToReturn = recordsToReturn.Select(fakeOrganizationService.ConvertToProxyType).ToList();

        var response = new RetrieveMultipleResponse
        {
            Results = new ParameterCollection
            {
                {
                    "EntityCollection",
                    new EntityCollection(recordsToReturn) { EntityName = entityName, MoreRecords = startPosition + numberToGet < internalResult.Count, TotalRecordCount = totalRecordCount }
                }
            }
        };

        if (!response.EntityCollection.MoreRecords) return response;

        var first = response.EntityCollection.Entities.First();
        var last = response.EntityCollection.Entities.Last();
        response.EntityCollection.PagingCookie =
            $"""<cookie page="{pageNumber}"><{first.LogicalName}id last="{last.Id.ToString("B").ToUpper()}" first="{first.Id.ToString("B").ToUpper()}" /></cookie>""";

        return response;
    }

    /// <summary>
    /// Parses a string containing FetchXML and returns an XDocument object.
    /// </summary>
    /// <param name="fetchXml">The FetchXML string to parse.</param>
    /// <returns>An XDocument object representing the parsed FetchXML.</returns>
    /// <exception cref="Exception">Thrown when the input string is not a valid XML document.</exception>
    private static XDocument ParseXml(string fetchXml)
    {
        try
        {
            // Parse the input string into an XDocument object
            return XDocument.Parse(fetchXml);
        }
        catch (Exception ex)
        {
            // If parsing fails, throw an exception with a helpful error message
            ErrorFactory.ThrowFault(ErrorCodes.InvalidArgument, $"FetchXml must be a valid XML document: {ex.Message}");
            return null!; // unreachable
        }
    }

    /// <summary>
    /// Returns a new list containing only the distinct entities from the input list.
    /// Two entities are considered distinct if they have the same logical name and all their attributes are equal.
    /// </summary>
    /// <param name="entities">The list of entities to filter.</param>
    /// <returns>A new list containing only the distinct entities from the input list.</returns>
    private static List<Entity> GetDistinctEntities(List<Entity> entities)
    {
        var output = new List<Entity>();

        foreach (var entity in entities.Where(entity => !output.Exists(i =>
                     i.LogicalName == entity.LogicalName && i.Attributes.Count == entity.Attributes.Count &&
                     i.Attributes.All(a => entity.Attributes.TryGetValue(a.Key, out var val) && Equals(a.Value, val)))))
        {
            output.Add(entity);
        }

        return output;
    }

    private static void PatchDateFormat(Entity record, FakeOrganizationService fakeOrganizationService)
    {
        if (!fakeOrganizationService.State.EntityMetadata.TryGetValue(record.LogicalName, out var entityMetadata)) return;
        if (entityMetadata.Attributes == null) return;

        foreach (var dateTimeAttribute in record.Attributes.Where(a => a.Value is DateTime).ToList())
        {
            if (entityMetadata.Attributes.SingleOrDefault(a => a.LogicalName == dateTimeAttribute.Key) is not DateTimeAttributeMetadata { Format: DateTimeFormat.DateOnly }) continue;

            var currentValue = (DateTime)record[dateTimeAttribute.Key];
            record[dateTimeAttribute.Key] = new DateTime(currentValue.Year, currentValue.Month, currentValue.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    private static void FillFormattedValues(Entity record)
    {
        // Iterate through attributes and retrieve formatted values based on type
        foreach (var attributeName in record.Attributes.Keys)
        {
            var value = record[attributeName];
            if (record.FormattedValues.ContainsKey(attributeName) || value == null) continue;

            if (TryGetFormattedValueForValue(value, out var formattedValue))
            {
                record.FormattedValues.Add(attributeName, formattedValue);
            }
        }
    }

    private static bool TryGetFormattedValueForValue(object value, out string formattedValue)
    {
        var result = false;
        formattedValue = string.Empty;

        switch (value)
        {
            case Enum:
                // Retrieve the enum type
                formattedValue = Enum.GetName(value.GetType(), value)!;
                result = true;
                break;
            case AliasedValue aliasedValue:
                result = TryGetFormattedValueForValue(aliasedValue.Value, out formattedValue);
                break;
        }

        return result;
    }
}
