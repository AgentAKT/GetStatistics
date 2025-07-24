namespace GetStatistics.Models
{
    // Models/FilterParameters.cs
    public class FilterParameters
    {
        public string Filter_One { get; set; }
        public string Filter_Two { get; set; }
        public string SearchText_One { get; set; }
        public string SearchText_Two { get; set; }

        public bool HasAnyFilter =>
            !string.IsNullOrEmpty(Filter_One) ||
            !string.IsNullOrEmpty(Filter_Two) ||
            !string.IsNullOrEmpty(SearchText_One) ||
            !string.IsNullOrEmpty(SearchText_Two);
    }
}