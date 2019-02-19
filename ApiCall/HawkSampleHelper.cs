namespace ApiCall
{
  using System;
  using System.Security.Cryptography;
  using System.Text;

  public static class HawkSampleHelper
  {
    // Calculates Hawk an authorization header
    public static string GetAuthorizationHeader(string method, Uri uri, string credentialId, string credentialKey, DateTime ts, TimeSpan timeSkewAdjustSeconds, string nonce = null)
    {
      var normalizedTs = ConvertToUnixTimestamp(ts + timeSkewAdjustSeconds).ToString();

      var host = uri.Host + (uri.Port != 80 ? ":" + uri.Port : string.Empty);

      if (string.IsNullOrEmpty(nonce))
      {
        nonce = GetRandomString(6);
      }

      var mac = CalculateMac(
        host,
        method,
        uri,
        normalizedTs,
        nonce,
        credentialKey);

      return string.Format(
        "Hawk id=\"{0}\", ts=\"{1}\", nonce=\"{2}\", mac=\"{3}\", ext=\"\"",
        credentialId,
        normalizedTs,
        nonce,
        mac);
    }

    // Calculates a mac value based on SHA-256
    public static string CalculateMac(string host, string method, Uri uri, string ts, string nonce, string credentialKey)
    {
      using (var hmac = HMAC.Create("HMACSHA256"))
      {
        hmac.Key = Encoding.UTF8.GetBytes(credentialKey);

        var sanitizedHost = host.IndexOf(':') > 0 ? host.Substring(0, host.IndexOf(':')) : host;

        var normalized = "hawk.1.header\n" +
          ts + "\n" +
          nonce + "\n" +
          method.ToUpper() + "\n" +
          uri.PathAndQuery + "\n" +
          sanitizedHost.ToLower() + "\n" +
          uri.Port.ToString() + "\n" +
          "\n" + // payload hash not used
          "\n"; // extension value not used

        var messageBytes = Encoding.UTF8.GetBytes(normalized);

        return Convert.ToBase64String(hmac.ComputeHash(messageBytes));
      }
    }

    // Converts a time into unix format
    private static long ConvertToUnixTimestamp(DateTime date)
    {
      var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      var diff = date.ToUniversalTime() - origin;
      return Convert.ToInt64(Math.Floor(diff.TotalSeconds));
    }

    // Calculates a random string
    private static string GetRandomString(int size)
    {
      const string RandomSource = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

      var result = new StringBuilder();
      var random = new Random();

      for (var i = 0; i < size; ++i)
      {
        result.Append(RandomSource[random.Next(RandomSource.Length)]);
      }

      return result.ToString();
    }
  }
}
