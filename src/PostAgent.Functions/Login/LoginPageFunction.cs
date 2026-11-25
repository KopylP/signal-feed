using System.Net;
using System.Security.Cryptography;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace PostAgent.Login;

public sealed class LoginPageFunction(LoginForm form)
{
    // Anonymous on purpose: Telegram opens the Mini App without a function key. LoginForm checks the signed initData instead.
    [Function("LoginPage")]
    public async Task<HttpResponseData> Page(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = LoginPageUrl.Route)] HttpRequestData request)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        response.Headers.Add("Content-Security-Policy", LoginPage.ContentSecurityPolicy(nonce));
        AddCommonHeaders(response);
        await response.WriteStringAsync(LoginPage.Html(nonce));
        return response;
    }

    [Function("LoginSubmit")]
    public async Task<HttpResponseData> Submit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = LoginPageUrl.Route)] HttpRequestData request,
        CancellationToken ct)
    {
        var response = request.CreateResponse(await form.HandleAsync(request.Body, ct));
        AddCommonHeaders(response);
        return response;
    }

    private static void AddCommonHeaders(HttpResponseData response)
    {
        response.Headers.Add("Cache-Control", "no-store");
        response.Headers.Add("Referrer-Policy", "no-referrer");
        response.Headers.Add("X-Content-Type-Options", "nosniff");
    }
}
