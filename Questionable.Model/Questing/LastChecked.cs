namespace Questionable.Model.Questing
{
    public class LastChecked
    {
        public string? Username { get; set; }
        //[JsonConverter(typeof(DateConverter))]
        public string? Date { get; set; }
    }
}