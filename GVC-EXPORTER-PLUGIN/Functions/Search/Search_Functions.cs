using Autodesk.Navisworks.Api;

namespace GVC_EXPORTER_PLUGIN.Functions.Search_
{
    public static class Search_Functions
    {
        public static ModelItemCollection SearchQuadrant()
        {
            Document doc = Application.ActiveDocument;
            Search search = new Search();
            search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;
            SearchCondition condition = SearchCondition.HasPropertyByDisplayName("Custom", "QUADRANTE");
            search.SearchConditions.Add(condition);
            ModelItemCollection results = search.FindAll(doc, false);

            return results;
        }

        public static ModelItemCollection GlobalSearch()
        {
            Document doc = Application.ActiveDocument;
            Search search = new Search();
            search.Selection.SelectAll();
            search.Locations = SearchLocations.DescendantsAndSelf;

            search.SearchConditions.Add(
                    SearchCondition.HasPropertyByDisplayName("Item", "Name").CompareWith(SearchConditionComparison.NotEqual, VariantData.FromBoolean(false))
                );

            ModelItemCollection results = search.FindAll(doc, false);

            return results;
        }
    }
}
