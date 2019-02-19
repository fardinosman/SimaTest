using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Helpers;

namespace ApiCall
{
  public class Program
  {
    const string myId = ""; // SupplierID
    const string mySecret = ""; // APiKey;

    static DateTime machineUtcDateTime;
    static TimeSpan timeSkewAdjustSeconds;
    
    static void Main(string[] args)
    {
      RunAsConsoleApplication();
    }

    private static void RunAsConsoleApplication()
    {
      bool shutdown = false;

      var client = new HttpClient();
      machineUtcDateTime = new DateTime();
      timeSkewAdjustSeconds = new TimeSpan();

      // Make a warm-up request only the first time 
      WarmUpRequest(client);
      
      RunApiCall(client, machineUtcDateTime, timeSkewAdjustSeconds);

      while (!shutdown)
      {
        Thread.Sleep(1000);
      }
    }

    private static void RunApiCall(HttpClient client, DateTime machineUtcDateTime, TimeSpan timeSkewAdjustSeconds)
    {
      Console.WriteLine("Running API get call");

      // After the warm-up request, we calculate an authorization header for each request, adjusting the time on the local machine to the servers
      var requestUri = new Uri("https://api.oline.dk/v1/SupplierServices/Properties");
      var requestHeader = CreateRequestHeader(machineUtcDateTime, ref timeSkewAdjustSeconds, requestUri);

      var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
      request.Headers.Add("Authorization", requestHeader);

      var response = client.SendAsync(request).Result;

      // In case of StatusCode 401 we need to start over with warmup request and timeSkewAdjustSeconds
      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        // Make a warm-up request again
        WarmUpRequest(client);

        // Perform request once more with new RequestHeader
        var requestHeader2 = CreateRequestHeader(machineUtcDateTime, ref timeSkewAdjustSeconds, requestUri);
        var request2 = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request2.Headers.Add("Authorization", requestHeader2);

        var response2 = client.SendAsync(request2).Result;
        if (response2.StatusCode == HttpStatusCode.Unauthorized)
        {
          // DO STUFF
        }
      }
      else
      {
        var content = response.Content.ReadAsStringAsync();
        Console.WriteLine(content.Result);
      }
    }

    private static string CreateRequestHeader(DateTime machineUtcDateTime, ref TimeSpan timeSkewAdjustSeconds, Uri requestUri)
    {
      var requestHeader = HawkSampleHelper.GetAuthorizationHeader(
        "get",
        requestUri,
        myId,
        mySecret,
        machineUtcDateTime,
        timeSkewAdjustSeconds);
      return requestHeader;
    }

    public static void WarmUpRequest(HttpClient client)
    {
      machineUtcDateTime = DateTime.UtcNow;

      // Pretend the machine time is 30 minutes behind
      machineUtcDateTime = machineUtcDateTime.AddMinutes(-30);

      // Make a warm-up request only the first time
      var warmupRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.oline.dk/v1/system/status");
      var warmupResponse = client.SendAsync(warmupRequest).Result;

      // Take the returned warm-up response
      dynamic data = Json.Decode(warmupResponse.Content.ReadAsStringAsync().Result);

      if (data.Offline)
      {
        Console.WriteLine("The service is offline with the following message: ");
        Console.WriteLine(data.OffLineMessage);
      }

      // The server responded, now use the returned ServerUtcTime to adjust the local machine time
      DateTime serverUtcDateTime;
      DateTime.TryParseExact(data.ServerUtcTime, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out serverUtcDateTime);

      // Calculate the difference between the current machine time and the server time
      timeSkewAdjustSeconds = serverUtcDateTime - machineUtcDateTime;
    }
  }
}
