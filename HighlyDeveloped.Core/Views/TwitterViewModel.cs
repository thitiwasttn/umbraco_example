namespace HighlyDeveloped.Core.Views
{
    /* this will hold the twitter data for redering the latest tweets
     */
    public class TwitterViewModel
    {
        public string TwitterHandle { get; set; }
        public bool Error { get; set; }
        public string Json { get; set; }
        public string Message { get; set; }
    }
}