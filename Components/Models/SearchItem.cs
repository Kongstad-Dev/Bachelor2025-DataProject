namespace BlazorTest.Models
{
    public class SearchItem
    {
        public string Id { get; set; }
        public string DisplayText { get; set; }
        
        public object Data { get; set; }
        
        // Add equality comparison for Contains() to work correctly
        public override bool Equals(object obj)
        {
            if (obj is SearchItem other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }
}