using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Carcheck.Api;

public sealed class LineMessagingClient(HttpClient httpClient,IConfiguration configuration)
{
    private readonly HttpClient http=httpClient;
    private readonly IConfiguration config=configuration;

    public bool Configured=>!string.IsNullOrWhiteSpace(config["Line:ChannelSecret"])&&!string.IsNullOrWhiteSpace(config["Line:ChannelAccessToken"]);

    public bool VerifySignature(byte[] body,string? signature)
    {
        var secret=config["Line:ChannelSecret"];
        if(string.IsNullOrWhiteSpace(secret)||string.IsNullOrWhiteSpace(signature))return false;
        byte[] supplied;try{supplied=Convert.FromBase64String(signature);}catch{return false;}
        var expected=HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),body);
        return supplied.Length==expected.Length&&CryptographicOperations.FixedTimeEquals(supplied,expected);
    }

    public async Task PushAsync(string lineUserId,string message,CancellationToken token)
        =>await SendAsync("https://api.line.me/v2/bot/message/push",new{to=lineUserId,messages=new[]{new{type="text",text=message}}},token);

    public async Task ReplyAsync(string replyToken,string message,CancellationToken token)
        =>await SendAsync("https://api.line.me/v2/bot/message/reply",new{replyToken,messages=new[]{new{type="text",text=message}}},token);

    private async Task SendAsync(string url,object payload,CancellationToken token)
    {
        var accessToken=config["Line:ChannelAccessToken"]??throw new InvalidOperationException("ยังไม่ได้ตั้งค่า LINE Channel Access Token");
        using var request=new HttpRequestMessage(HttpMethod.Post,url);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
        request.Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
        using var response=await http.SendAsync(request,token);
        if(response.IsSuccessStatusCode)return;
        var detail=await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException($"LINE API ตอบกลับ {(int)response.StatusCode}: {detail[..Math.Min(detail.Length,300)]}");
    }

    public static string TaxMessage(LineSendInput item)
    {
        var thaiDate=item.DueDate.ToString("d MMMM yyyy",new System.Globalization.CultureInfo("th-TH"));
        var days=(item.DueDate.Date-DateTime.Today).Days;
        var remaining=days>=0?$"เหลืออีก {days} วัน กรุณานำรถเข้ารับบริการก่อนครบกำหนด":"ครบกำหนดแล้ว กรุณาติดต่อศูนย์บริการ";
        return $"🔔 แจ้งเตือนภาษีรถยนต์ใกล้หมดอายุ\n\nเรียน คุณ{item.CustomerName}\n\nรถทะเบียน {item.LicensePlate} {item.Province}\nวันหมดอายุภาษี: {thaiDate}\n\n{remaining}\n\n{item.CenterName}\nขอบคุณที่ใช้บริการครับ";
    }
}
