﻿using Examine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Mvc;

namespace Vendr.DemoStore.Web.Controllers
{
    public class SearchSurfaceController : SurfaceController
    {
        private readonly IExamineManager _examineManager;
        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        public SearchSurfaceController(IExamineManager examineManager, IUmbracoContextAccessor umbracoContextAccessor)
        {
            _examineManager = examineManager;
            _umbracoContextAccessor = umbracoContextAccessor;
        }

        [ChildActionOnly]
        public ActionResult Search(string q = "", int p = 1, int ps = 12)
        {
            // The logic for searching is mostly pulled from ezSearch
            // https://github.com/umco/umbraco-ezsearch/blob/master/Src/Our.Umbraco.ezSearch/Web/UI/Views/MacroPartials/ezSearch.cshtml

            var result = new PagedResult<IPublishedContent>(0, 1, ps);

            if (!q.IsNullOrWhiteSpace() && _examineManager.TryGetIndex("ExternalIndex", out var index))
            {
                var searchTerms = Tokenize(q);
                var searchFields = new[] { "nodeName", "metaTitle", "description", "shortDescription", "longDescription", "metaDescription", "bodyText", "content" };

                var searcher = index.GetSearcher();
                var query = new StringBuilder();

                query.Append("+__IndexType:content "); // Must be content
                query.Append("-templateID:0 "); // Must have a template
                query.Append("-umbracoNaviHide:1 "); // Must no be hidden

                // Ensure page contains all search terms in some way
                foreach (var term in searchTerms)
                {
                    var groupedOr = searchFields.Aggregate(new StringBuilder(), (innerQuery, searchField) =>
                    {
                        var format = searchField.Contains(" ") ? @"{0}:""{1}"" " : "{0}:{1}* ";
                        innerQuery.AppendFormat(format, searchField, term);
                        return innerQuery;
                    });

                    query.Append("+(" + groupedOr.ToString() + ") ");
                }

                // Rank content based on positon of search terms in fields
                for(var i = 0; i < searchFields.Length; i++) 
                {
                    foreach (var term in searchTerms)
                    {
                        var searchField = searchFields[i];
                        var format = searchField.Contains(" ") ? @"{0}:""{1}""^{2} " : "{0}:{1}*^{2} ";
                        query.AppendFormat(format, searchField, term, searchFields.Length - i);
                    }
                }

                var examineQuery = searcher.CreateQuery().NativeQuery(query.ToString());
                var results = examineQuery.Execute(ps * p);
                var totalResults = results.TotalItemCount;
                var pagedResults = results.Skip(ps * (p - 1));

                var items = pagedResults.ToPublishedSearchResults(_umbracoContextAccessor.UmbracoContext.Content)
                                        .Select(x => x.Content);

                result = new PagedResult<IPublishedContent>(totalResults, p, ps)
                {
                    Items = items
                };
            }

            return PartialView("SearchResults", result);
        }

        public IEnumerable<string> Tokenize(string input)
        {
            return Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
                .Cast<Match>()
                .Select(m => m.Value.Trim('\"').ToLower())
                .ToList();
        }
    }
}
