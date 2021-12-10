using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using HighlyDeveloped.Core.Views;
using Serilog;
using Umbraco.Web;
using Umbraco.Web.Mvc;

namespace HighlyDeveloped.Core.Controllers
{
    public class TwitterController : SurfaceController
    {
        public ActionResult GetTweets(string twitterHandle)
        {
            string PARTIAL_VIEW_FOLDER = "~/Views/Partials";

            //Create the view model
            var vm = new TwitterViewModel();
            vm.TwitterHandle = twitterHandle;
            // Grab the tweet data from twitter API
            try
            {
                var tweets = GetLatestTweets(twitterHandle, 4);
                vm.Json = tweets;
                vm.Error = false;
            }
            catch (Exception ex)
            {
                vm.Error = true;
                vm.Message = ex.Message + ex.StackTrace;
            }

            // Return the view
            return PartialView(PARTIAL_VIEW_FOLDER + "/LatestTweets.cshtml", vm);
        }

        private string GetLatestTweets(string twitterHandle, int numberOfTweets)
        {
            // get the key from site settings

            var siteSettings = Umbraco.ContentQuery
                .ContentAtRoot()
                .DescendantsOrSelfOfType("siteSettings")
                .FirstOrDefault();

            if (siteSettings == null)
            {
                throw new Exception("No site Settings");
            }

            var oauth_token = siteSettings.Value<string>("twitterAccessToken");
            var oauth_token_secret = siteSettings.Value<string>("twitterAccessTokenSecret");
            var oauth_consumer_key = siteSettings.Value<string>("twitterConsumerAPIKey");
            var oauth_consumer_secret = siteSettings.Value<string>("twitterConsumerSecretAPIKey");
            // get the data from twitter
            var excludeReplies = "true";
            var trimUser = "true";
            var extendedTweets = "extended";

            //oauth
            var oauth_version = "1.0";
            var oauth_signature_method = "HMAC-SHA1";
            var oauth_nonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
            var timeSpan = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var oauth_timestamp = Convert.ToInt64(timeSpan.TotalSeconds).ToString();

            //construct url
            var resource_url = "https://api.twitter.com/1.1/statuses/user_timeline.json";

            //encrypted oAuth signature
            var baseFormat =
                "count={0}&exclude_replies={1}&oauth_consumer_key={2}&oauth_nonce={3}&oauth_signature_method={4}" +
                "&oauth_timestamp={5}&oauth_token={6}&oauth_version={7}&screen_name={8}&trim_user={9}&tweet_mode={10}";

            var baseString = string.Format(baseFormat,
                numberOfTweets,
                excludeReplies,
                oauth_consumer_key,
                oauth_nonce,
                oauth_signature_method,
                oauth_timestamp,
                oauth_token,
                oauth_version,
                twitterHandle,
                trimUser,
                extendedTweets
            );

            baseString = string.Concat("GET&", Uri.EscapeDataString(resource_url),
                "&", Uri.EscapeDataString(baseString));

            //Encrypt data

            var compositeKey = string.Concat(Uri.EscapeDataString(oauth_consumer_secret),
                "&", Uri.EscapeDataString(oauth_token_secret));

            string oauth_signature;
            using (HMACSHA1 hasher = new HMACSHA1(ASCIIEncoding.ASCII.GetBytes(compositeKey)))
            {
                oauth_signature = Convert.ToBase64String(hasher.ComputeHash(ASCIIEncoding.ASCII.GetBytes(baseString)));
            }


            var headerFormat = "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", " +
                               "oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", " +
                               "oauth_token=\"{4}\", oauth_signature=\"{5}\", " +
                               "oauth_version=\"{6}\"";

            var authHeader = string.Format(headerFormat,
                Uri.EscapeDataString(oauth_nonce),
                Uri.EscapeDataString(oauth_signature_method),
                Uri.EscapeDataString(oauth_timestamp),
                Uri.EscapeDataString(oauth_consumer_key),
                Uri.EscapeDataString(oauth_token),
                Uri.EscapeDataString(oauth_signature),
                Uri.EscapeDataString(oauth_version)
            );


            ServicePointManager.Expect100Continue = false;
            var postBody = "screen_name=" + twitterHandle;
            postBody = postBody + string.Format("&count={0}&trim_user={1}&exclude_replies={2}&tweet_mode={3}",
                numberOfTweets, trimUser, excludeReplies, extendedTweets);
            resource_url += "?" + postBody;


            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(resource_url);
            request.Headers.Add("Authorization", authHeader);
            request.Method = "GET";
            request.ContentType = "application/x-www-form-urlencoded";
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            WebResponse response = request.GetResponse();
            string responseData;

            //read from twitter
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                responseData = reader.ReadToEnd();
            }

            return responseData;
        }
    }
}