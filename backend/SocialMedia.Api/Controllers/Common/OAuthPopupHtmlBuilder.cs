using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SocialMedia.Application.DTOs.Integration;

namespace SocialMedia.Api.Controllers.Common;

public static class OAuthPopupHtmlBuilder
{
    public static string Build(MetaRedirectResult result)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "smh-meta-oauth",
            platform = result.PlatformCode,
            ok = result.Ok,
            message = result.Message
        });

        var originsJson = JsonSerializer.Serialize(result.FrontendOrigins);
        var statusText = System.Net.WebUtility.HtmlEncode(result.Message);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>SocialHub Connect</title>
              <style>
                body { font-family: Segoe UI, system-ui, sans-serif; display: grid; place-items: center;
                       min-height: 100vh; margin: 0; background: #f8fafc; color: #1e293b; text-align: center; }
              </style>
            </head>
            <body>
              <p>{{statusText}}</p>
              <script>
                (function () {
                  var payload = {{payload}};
                  var origins = {{originsJson}};
                  if (window.opener) {
                    for (var i = 0; i < origins.length; i++) {
                      try { window.opener.postMessage(payload, origins[i]); } catch (e) {}
                    }
                  }
                  if (window.BroadcastChannel) {
                    try {
                      var channel = new BroadcastChannel('smh-meta-oauth');
                      channel.postMessage(payload);
                      channel.close();
                    } catch (e) {}
                  }
                  setTimeout(function () { window.close(); }, 700);
                })();
              </script>
            </body>
            </html>
            """;
    }

    public static ContentResult AsHtml(MetaRedirectResult result)
        => new ContentResult
        {
            Content = Build(result),
            ContentType = "text/html",
            StatusCode = 200
        };
}
