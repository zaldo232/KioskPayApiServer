using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 메모리 캐시 (실서비스는 DB 권장)
Dictionary<string, string> OrderIdToTid = new();

app.MapGet("/", () => "KakaoPay Approval Server Running!");

// 1. tid등록 엔드포인트 (★ WPF에서 반드시 POST!)
app.MapPost("/register-tid", async (HttpContext context) =>
{
    var data = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(context.Request.Body);
    if (data == null || !data.TryGetValue("orderId", out var orderId) || !data.TryGetValue("tid", out var tid))
        return Results.BadRequest("Missing orderId or tid");
    OrderIdToTid[orderId] = tid;
    Console.WriteLine($"[REGISTER] {orderId} -> {tid}");
    return Results.Ok();
});

// 2. approval_url 콜백 (카카오에서 GET)
app.MapGet("/approve", async (HttpContext context) =>
{
    string? pg_token = context.Request.Query["pg_token"];
    string? partner_order_id = context.Request.Query["orderId"];
    string partner_user_id = "kiosk-user";

    if (string.IsNullOrEmpty(pg_token) || string.IsNullOrEmpty(partner_order_id))
        return Results.BadRequest("Missing pg_token or orderId");

    if (!OrderIdToTid.TryGetValue(partner_order_id, out var tid) || string.IsNullOrEmpty(tid))
        return Results.BadRequest("tid not found for orderId");

    // 카카오페이 결제승인 API
    var httpClient = new HttpClient();
    var requestUrl = "https://kapi.kakao.com/v1/payment/approve";
    var adminKey = "0f96e6b0ba4e4797fb92766da78409f3";
    var cid = "TC0ONETIME";

    var parameters = new Dictionary<string, string>
    {
        { "cid", cid },
        { "tid", tid },
        { "partner_order_id", partner_order_id },
        { "partner_user_id", partner_user_id },
        { "pg_token", pg_token }
    };

    var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
    {
        Content = new FormUrlEncodedContent(parameters)
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("KakaoAK", adminKey);

    var response = await httpClient.SendAsync(request);
    var result = await response.Content.ReadAsStringAsync();

    if (response.IsSuccessStatusCode)
        return Results.Content("<html><body><h2>결제가 정상적으로 완료되었습니다.</h2></body></html>", "text/html");
    else
        return Results.Content($"<html><body><h2>결제 승인 실패!</h2><pre>{result}</pre></body></html>", "text/html");
});

app.Run();
 