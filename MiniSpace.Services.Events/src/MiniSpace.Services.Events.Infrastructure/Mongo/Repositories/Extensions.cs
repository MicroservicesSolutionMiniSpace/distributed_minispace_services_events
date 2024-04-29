﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MiniSpace.Services.Events.Application.DTO;
using MiniSpace.Services.Events.Core.Entities;
using MiniSpace.Services.Events.Infrastructure.Mongo.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MiniSpace.Services.Events.Infrastructure.Mongo.Repositories
{
    public static class Extensions
    {
        public static async Task<(int totalPages, int totalElements, IReadOnlyList<TDocument> data)> AggregateByPage<TDocument>(
            this IMongoCollection<TDocument> collection,
            FilterDefinition<TDocument> filterDefinition,
            SortDefinition<TDocument> sortDefinition,
            int page,
            int pageSize)
        {
            var countFacet = AggregateFacet.Create("count",
                PipelineDefinition<TDocument, AggregateCountResult>.Create(new[]
                {
                    PipelineStageDefinitionBuilder.Count<TDocument>()
                }));

            var dataFacet = AggregateFacet.Create("data",
                PipelineDefinition<TDocument, TDocument>.Create(new[]
                {
                    PipelineStageDefinitionBuilder.Sort(sortDefinition),
                    PipelineStageDefinitionBuilder.Skip<TDocument>((page - 1) * pageSize),
                    PipelineStageDefinitionBuilder.Limit<TDocument>(pageSize),
                }));


            var aggregation = await collection.Aggregate()
                .Match(filterDefinition)
                .Facet(countFacet, dataFacet)
                .ToListAsync();

            var count = aggregation.First()
                .Facets.First(x => x.Name == "count")
                .Output<AggregateCountResult>()
                ?.FirstOrDefault()
                ?.Count;

            var totalPages = (int)Math.Ceiling((double)count/ pageSize);

            var data = aggregation.First()
                .Facets.First(x => x.Name == "data")
                .Output<TDocument>();

            return (totalPages, (int)count, data);
        }
        
        public static FilterDefinition<EventDocument> ToFilterDefinition(string name, string organizer, 
            DateTime dateFrom, DateTime dateTo, IEnumerable<Guid> eventIds = null)
        {
            var filterDefinitionBuilder = Builders<EventDocument>.Filter;
            var filterDefinition = filterDefinitionBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(name))
            {
                filterDefinition &= filterDefinitionBuilder.Regex(x => x.Name,
                    new BsonRegularExpression(new Regex($".*{name}.*", RegexOptions.IgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(organizer))
            {   
                filterDefinition &= filterDefinitionBuilder.Regex(x => x.Organizer.OrganizationName, 
                    new BsonRegularExpression(new Regex($".*{organizer}.*", RegexOptions.IgnoreCase)));
            }

            if (dateFrom != DateTime.MinValue)
            {
                filterDefinition &= filterDefinitionBuilder.Gte(x => x.StartDate, dateFrom);
            }

            if (dateTo != DateTime.MinValue)
            {
                filterDefinition &= filterDefinitionBuilder.Lte(x => x.EndDate, dateTo);
            }

            if (eventIds != null)
            {
                filterDefinition &= filterDefinitionBuilder.In(x => x.Id, eventIds);
            }

            return filterDefinition;
        }
        
        public static void AddOrganizerIdFilter (this FilterDefinition<EventDocument> filterDefinition, Guid organizerId)
        {
            var filterDefinitionBuilder = Builders<EventDocument>.Filter;
            filterDefinition &= filterDefinitionBuilder.Eq(x => x.Organizer.Id, organizerId);
        }
        
        public static void AddStateFilter (this FilterDefinition<EventDocument> filterDefinition, State? state)
        {
            if (state == null)
            {
                return;
            }
            var filterDefinitionBuilder = Builders<EventDocument>.Filter;
            filterDefinition &= filterDefinitionBuilder.Eq(x => x.State, state);
        }
        
        public static SortDefinition<EventDocument> ToSortDefinition(IEnumerable<string> sortByArguments, string direction)
        {
            var sort = sortByArguments.ToList();
            if(!sort.Any())
            {
                sort.Add("StartDate");
            }
            var sortDefinitionBuilder = Builders<EventDocument>.Sort;
            var sortDefinition = sort
                .Select(sortBy => direction == "asc"
                    ? sortDefinitionBuilder.Ascending(sortBy)
                    : sortDefinitionBuilder.Descending(sortBy));
            var sortCombined = sortDefinitionBuilder.Combine(sortDefinition);

            return sortCombined;
        }
    }
}