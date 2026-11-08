using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Text.Json;
using Carcheck.Api;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var origins = builder.Configuration.GetSection("Api:AllowedOrigins").GetChildren().Select(item => item.Value).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
builder.Services.AddCors(options => options.AddPolicy("Web", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddSingleton<LegacySqlRepository>();
builder.Services.AddSingleton<LoginSessionStore>();
builder.Services.AddHttpClient<LineMessagingClient>();
builder.Services.AddHostedService<LineReminderWorker>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseCors("Web");
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/api/health")
    {
        await next();
        return;
    }
    var localOrigin = context.Request.Headers.Origin.ToString() == "http://127.0.0.1:5173";
    var loopback = context.Connection.RemoteIpAddress is not null && IPAddress.IsLoopback(context.Connection.RemoteIpAddress);
    var localDevelopment = app.Configuration.GetValue<bool>("Api:AllowLocalDevelopmentWithoutToken") && localOrigin && loopback;
    var publicRead = context.Request.Path.StartsWithSegments("/api/public");
    var publicAuthentication = context.Request.Path == "/api/auth/login" || context.Request.Path == "/api/auth/session";
    var publicWebhook = context.Request.Path == "/api/line/webhook";
    var sessions = context.RequestServices.GetRequiredService<LoginSessionStore>();
    var hasSession = sessions.TryGet(context.Request.Cookies["carcheck.session"], out _);
    if (!localDevelopment && !publicRead && !publicAuthentication && !publicWebhook && !hasSession)
    {
        var expected = app.Configuration["Api:Token"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            await Results.Problem("API token ยังไม่ได้ตั้งค่าบน server", statusCode: 503).ExecuteAsync(context);
            return;
        }
        var supplied = context.Request.Headers.Authorization.ToString();
        if (!supplied.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await Results.Unauthorized().ExecuteAsync(context);
            return;
        }
        var actual = supplied[7..].Trim();
        var valid = actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));
        if (!valid)
        {
            await Results.Unauthorized().ExecuteAsync(context);
            return;
        }
    }
    if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
    {
        await Results.Problem("ยังไม่ได้ตั้งค่า ConnectionStrings__DefaultConnection", statusCode: 503).ExecuteAsync(context);
        return;
    }
    await next();
});

app.MapGet("/api/health", (IConfiguration config) => Results.Ok(new
{
    status = "ok",
    databaseConfigured = !string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")),
    mode = "read-only"
}));

var api = app.MapGroup("/api");
const string sessionCookie="carcheck.session";
await app.Services.GetRequiredService<LegacySqlRepository>().EnsureAttendanceTableAsync(CancellationToken.None);
await app.Services.GetRequiredService<LegacySqlRepository>().EnsureLineTablesAsync(CancellationToken.None);
api.MapGet("/attendance",async(string centerId,LegacySqlRepository repository,CancellationToken token)=>string.IsNullOrWhiteSpace(centerId)?Results.BadRequest(new{error="ต้องระบุศูนย์บริการ"}):Results.Ok(await repository.GetAttendanceAsync(centerId.Trim(),token)));
api.MapPost("/attendance",async(AttendanceInput input,LegacySqlRepository repository,CancellationToken token)=>string.IsNullOrWhiteSpace(input.PersonKey)||string.IsNullOrWhiteSpace(input.CenterId)||!DateOnly.TryParse(input.Day,out _)?Results.BadRequest(new{error="ข้อมูลลงเวลาไม่ครบถ้วน"}):Results.Ok(await repository.SaveAttendanceAsync(input,token)));
api.MapGet("/line/status",(LineMessagingClient line,IConfiguration configuration)=>Results.Ok(new{configured=line.Configured,automatic=configuration.GetValue<bool>("Line:AutomaticEnabled"),leadDays=configuration.GetValue("Line:LeadDays",30),sendHour=configuration.GetValue("Line:SendHour",9)}));
api.MapPost("/line/link-code",async(LineLinkCodeInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    if(string.IsNullOrWhiteSpace(input.CustomerId))return Results.BadRequest(new{error="กรุณาระบุรหัสลูกค้า"});
    var customers=await repository.GetCustomersAsync(input.CustomerId.Trim(),token);if(!customers.Any(customer=>customer.Id==input.CustomerId.Trim()))return Results.NotFound(new{error="ไม่พบรหัสลูกค้า"});
    return Results.Ok(await repository.CreateLineLinkCodeAsync(input.CustomerId.Trim(),input.CreatedBy?.Trim()??string.Empty,token));
});
api.MapDelete("/line/customers/{customerId}",async(string customerId,LegacySqlRepository repository,CancellationToken token)=>await repository.UnlinkLineCustomerAsync(customerId.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบการผูก LINE ของลูกค้า"}));
api.MapPost("/line/send-tax",async(LineSendInput input,LegacySqlRepository repository,LineMessagingClient line,CancellationToken token)=>
{
    if(!line.Configured)return Results.Json(new{error="ยังไม่ได้ตั้งค่า LINE Channel Secret และ Access Token"},statusCode:503);
    var lineUser=await repository.GetLineUserIdAsync(input.CustomerId,token);if(lineUser is null)return Results.BadRequest(new{error="ลูกค้ายังไม่ได้ผูกบัญชี LINE"});
    if(!await repository.ReserveLineNotificationAsync(input.WorkId,input.CustomerId,input.DueDate,"TAX",token))return Results.Ok(new LineSendResult(false,"DUPLICATE","รายการนี้ส่ง LINE แล้ว"));
    try{await line.PushAsync(lineUser,LineMessagingClient.TaxMessage(input),token);await repository.CompleteLineNotificationAsync(input.WorkId,input.DueDate,"TAX",true,null,token);return Results.Ok(new LineSendResult(true,"SENT","ส่ง LINE เรียบร้อย"));}
    catch(Exception error){await repository.CompleteLineNotificationAsync(input.WorkId,input.DueDate,"TAX",false,error.Message,token);return Results.Json(new{error=error.Message},statusCode:502);}
});
api.MapPost("/line/webhook",async(HttpRequest request,LegacySqlRepository repository,LineMessagingClient line,CancellationToken token)=>
{
    using var memory=new MemoryStream();await request.Body.CopyToAsync(memory,token);var body=memory.ToArray();
    if(!line.VerifySignature(body,request.Headers["x-line-signature"].ToString()))return Results.Unauthorized();
    using var document=JsonDocument.Parse(body);
    if(!document.RootElement.TryGetProperty("events",out var events))return Results.Ok();
    foreach(var item in events.EnumerateArray())
    {
        var type=item.TryGetProperty("type",out var typeValue)?typeValue.GetString():null;
        var userId=item.TryGetProperty("source",out var source)&&source.TryGetProperty("userId",out var user)?user.GetString():null;
        if(string.IsNullOrWhiteSpace(userId))continue;
        if(type=="unfollow"){await repository.DeactivateLineUserAsync(userId,token);continue;}
        if(type=="follow"&&item.TryGetProperty("replyToken",out var followReply)){await line.ReplyAsync(followReply.GetString()!,"ยินดีต้อนรับครับ หากได้รับรหัสจากศูนย์บริการ ให้ส่งรหัส 8 ตัวได้ทันที เช่น ABCD2345",token);continue;}
        if(type!="message"||!item.TryGetProperty("message",out var message)||message.GetProperty("type").GetString()!="text")continue;
        var text=message.GetProperty("text").GetString()?.Trim()??string.Empty;
        var code=(text.StartsWith("ผูก ",StringComparison.OrdinalIgnoreCase)?text[4..]:text).Trim().ToUpperInvariant();if(code.Length!=8)continue;
        var customer=await repository.BindLineCustomerAsync(code,userId,token);
        if(item.TryGetProperty("replyToken",out var reply))await line.ReplyAsync(reply.GetString()!,customer is null?"รหัสไม่ถูกต้อง หมดอายุ หรือถูกใช้แล้ว กรุณาขอรหัสใหม่จากศูนย์บริการ":$"ผูกบัญชี LINE กับรหัสลูกค้า {customer} เรียบร้อยแล้วครับ",token);
    }
    return Results.Ok();
});
api.MapPost("/auth/login", async (LoginRequest request, HttpContext context, LegacySqlRepository repository, LoginSessionStore sessions, CancellationToken token) =>
{
    var username = request.Username?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;
    if (username.Length is < 1 or > 200 || password.Length is < 1 or > 200)
        return Results.BadRequest(new { error = "กรุณาระบุชื่อผู้ใช้และรหัสผ่าน" });
    var user = await repository.AuthenticateAsync(username, password, token);
    if(user is null)return Results.Json(new { error = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง" }, statusCode: StatusCodes.Status401Unauthorized);
    var session=sessions.Create(user);
    context.Response.Cookies.Append(sessionCookie,session.Token,SessionCookieOptions(context,session.Expires));
    return Results.Ok(user);
});
api.MapGet("/auth/session",(HttpContext context,LoginSessionStore sessions)=>sessions.TryGet(context.Request.Cookies[sessionCookie],out var user)?Results.Ok(user):Results.Unauthorized());
api.MapPost("/auth/logout",(HttpContext context,LoginSessionStore sessions)=>{sessions.Revoke(context.Request.Cookies[sessionCookie]);context.Response.Cookies.Delete(sessionCookie,SessionCookieOptions(context));return Results.NoContent();});
api.MapPut("/account/password",async(PasswordChangeInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    if(string.IsNullOrWhiteSpace(input.UserId))return Results.BadRequest(new{error="กรุณาเข้าสู่ระบบใหม่"});if(string.IsNullOrEmpty(input.CurrentPassword))return Results.BadRequest(new{error="กรุณาใส่รหัสผ่านเดิม"});if(string.IsNullOrEmpty(input.NewPassword))return Results.BadRequest(new{error="กรุณาใส่รหัสผ่านใหม่"});if(input.NewPassword!=input.ConfirmPassword)return Results.BadRequest(new{error="ยืนยันรหัสผ่านไม่ตรงกัน"});if(input.NewPassword.Length>200)return Results.BadRequest(new{error="รหัสผ่านต้องไม่เกิน 200 ตัวอักษร"});return await repository.ChangePasswordAsync(input.UserId,input.CurrentPassword,input.NewPassword,token)?Results.NoContent():Results.BadRequest(new{error="รหัสผ่านเดิมไม่ถูกต้อง"});
});
api.MapGet("/public/home", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetPublicHomeAsync(token)));
api.MapGet("/public/header-image", async (LegacySqlRepository repository, CancellationToken token) =>
{
    var asset=await repository.GetHeaderImageAsync(token);return asset is null?Results.NotFound():Results.File(asset.Data,asset.ContentType,enableRangeProcessing:false);
});
api.MapGet("/public/announcements/{id}", async (string id, LegacySqlRepository repository, CancellationToken token) =>
{
    if(string.IsNullOrWhiteSpace(id)||id.Length>2)return Results.BadRequest();var asset=await repository.GetAnnouncementImageAsync(id,token);return asset is null?Results.NotFound():Results.File(asset.Data,asset.ContentType,enableRangeProcessing:false);
});
api.MapGet("/public/customer-announcements/{id:int}",async(int id,LegacySqlRepository repository,IConfiguration configuration,CancellationToken token)=>
{
    var row=(await repository.GetCustomerAnnouncementsAsync(token)).FirstOrDefault(item=>item.Id==id&&item.Type.Contains('I',StringComparison.OrdinalIgnoreCase)&&item.HasImage);
    if(row is null)return Results.NotFound();
    var file=Path.Combine(configuration["Api:LegacyCustomerImageRoot"]??string.Empty,Path.GetFileName(row.Path));
    return File.Exists(file)?Results.File(file,DetectFileContentType(file),enableRangeProcessing:false):Results.NotFound();
});
api.MapGet("/public/website-entry/{id}",async(string id,LegacySqlRepository repository,CancellationToken token)=>
{
    if(string.IsNullOrWhiteSpace(id)||id.Length>50)return Results.BadRequest();
    var row=(await repository.GetWebsiteEntriesAsync(token)).FirstOrDefault(item=>item.Id==id&&item.Visible&&item.HasImage);
    if(row is null)return Results.NotFound();
    var asset=await repository.GetWebsiteEntryImageAsync(id,token);return asset is null?Results.NotFound():Results.File(asset.Data,asset.ContentType,enableRangeProcessing:false);
});
api.MapGet("/database/health", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.CheckConnectionAsync(token)));
api.MapGet("/database/summary", async (string? centerId, LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetSummaryAsync(centerId?.Trim(),token)));

api.MapGet("/jobs", async (string centerId, string? search, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุ centerId" });
    return Results.Ok(await repository.GetJobsAsync(centerId.Trim(), search, token));
});
api.MapPost("/jobs",async(JobInput input,LegacySqlRepository repository,CancellationToken token)=>ValidateJob(input) is { } error?Results.BadRequest(new{error}):Results.Created("/api/jobs",new{id=await repository.CreateJobAsync(input,token)}));
api.MapPut("/jobs/{id}",async(string id,JobInput input,LegacySqlRepository repository,CancellationToken token)=>ValidateJob(input) is { } error?Results.BadRequest(new{error}):await repository.UpdateJobAsync(id.Trim(),input,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบรายการรับงาน"}));
api.MapDelete("/jobs/{id}",async(string id,string centerId,LegacySqlRepository repository,CancellationToken token)=>await repository.DeleteJobAsync(id.Trim(),centerId.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบรายการรับงาน"}));
api.MapGet("/master/centers", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetCentersAsync(token)));
api.MapGet("/centers", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetCenterRecordsAsync(token)));
api.MapPost("/centers", async (CenterInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    var error = ValidateCenter(input); if (error is not null)return Results.BadRequest(new { error });
    return Results.Created("/api/centers", await repository.CreateCenterAsync(input, token));
});
api.MapPut("/centers/{id}", async (string id, CenterInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(id))return Results.BadRequest(new { error = "ไม่พบรหัสศูนย์บริการ" });
    var error = ValidateCenter(input); if (error is not null)return Results.BadRequest(new { error });
    return await repository.UpdateCenterAsync(id.Trim(), input, token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบศูนย์บริการที่ต้องการแก้ไข" });
});
api.MapDelete("/centers/{id}", async (string id, LegacySqlRepository repository, CancellationToken token) =>
    await repository.DeleteCenterAsync(id.Trim(), token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบศูนย์บริการที่ต้องการลบ" }));
api.MapGet("/master/insurers", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetInsurersAsync(token)));
api.MapGet("/insurers", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetInsurerRecordsAsync(token)));
api.MapPost("/insurers", async (InsurerInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    var error = ValidateInsurer(input); if (error is not null)return Results.BadRequest(new { error });
    return Results.Created("/api/insurers", await repository.CreateInsurerAsync(input, token));
});
api.MapPut("/insurers/{id}", async (string id, InsurerInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(id))return Results.BadRequest(new { error = "ไม่พบรหัสบริษัทประกัน" });
    var error = ValidateInsurer(input); if (error is not null)return Results.BadRequest(new { error });
    return await repository.UpdateInsurerAsync(id.Trim(), input, token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบบริษัทประกันที่ต้องการแก้ไข" });
});
api.MapDelete("/insurers/{id}", async (string id, LegacySqlRepository repository, CancellationToken token) =>
    await repository.DeleteInsurerAsync(id.Trim(), token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบบริษัทประกันที่ต้องการลบ" }));
api.MapGet("/master/vehicle-types", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetVehicleTypesAsync(token)));
api.MapGet("/vehicle-types", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetVehicleTypeRecordsAsync(token)));
api.MapPost("/vehicle-types", async (VehicleTypeInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    var error = ValidateVehicleType(input); if (error is not null)return Results.BadRequest(new { error });
    return Results.Created("/api/vehicle-types", await repository.CreateVehicleTypeAsync(input, token));
});
api.MapPut("/vehicle-types/{id}", async (string id, VehicleTypeInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(id))return Results.BadRequest(new { error = "ไม่พบรหัสประเภท รย." });
    var error = ValidateVehicleType(input); if (error is not null)return Results.BadRequest(new { error });
    return await repository.UpdateVehicleTypeAsync(id.Trim(), input, token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบประเภท รย. ที่ต้องการแก้ไข" });
});
api.MapDelete("/vehicle-types/{id}", async (string id, LegacySqlRepository repository, CancellationToken token) =>
    await repository.DeleteVehicleTypeAsync(id.Trim(), token) ? Results.NoContent() : Results.NotFound(new { error = "ไม่พบประเภท รย. ที่ต้องการลบ" }));
api.MapGet("/master/provinces", async (LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetProvincesAsync(token)));
api.MapGet("/master/address-options",async(LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetAddressOptionsAsync(token)));
api.MapGet("/staff", async (string? viewerId, LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetStaffRecordsAsync(viewerId?.Trim()??string.Empty, token)));
api.MapPost("/staff", async (StaffInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    var error=ValidateStaff(input,true); if(error is not null)return Results.BadRequest(new{error});
    return Results.Created("/api/staff",await repository.CreateStaffAsync(input,token));
});
api.MapPut("/staff/{id}", async (string id, StaffInput input, LegacySqlRepository repository, CancellationToken token) =>
{
    if(string.IsNullOrWhiteSpace(id))return Results.BadRequest(new{error="ไม่พบรหัสพนักงาน"});
    var error=ValidateStaff(input,false); if(error is not null)return Results.BadRequest(new{error});
    return await repository.UpdateStaffAsync(id.Trim(),input,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบพนักงานที่ต้องการแก้ไข"});
});
api.MapDelete("/staff/{id}", async (string id, LegacySqlRepository repository, CancellationToken token) =>
    await repository.DeleteStaffAsync(id.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบพนักงานที่ต้องการลบ"}));
api.MapGet("/permissions", async (string? viewerId,LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetPermissionsAsync(viewerId?.Trim()??string.Empty,token)));
api.MapPost("/permissions/{userId}", async (string userId,PermissionInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var error=ValidatePermission(userId,input);if(error is not null)return Results.BadRequest(new{error});
    await repository.SavePermissionsAsync(userId.Trim(),input.ProgramIds,token);return Results.NoContent();
});
api.MapPut("/permissions/{userId}", async (string userId,PermissionInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var error=ValidatePermission(userId,input);if(error is not null)return Results.BadRequest(new{error});
    await repository.SavePermissionsAsync(userId.Trim(),input.ProgramIds,token);return Results.NoContent();
});
api.MapDelete("/permissions/{userId}",async(string userId,LegacySqlRepository repository,CancellationToken token)=>
    await repository.DeletePermissionsAsync(userId.Trim(),token)?Results.NoContent():Results.NotFound(new{error="พนักงานนี้ยังไม่มีสิทธิ์เมนูให้ลบ"}));
api.MapGet("/internal-announcements",async(LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetInternalAnnouncementsAsync(token)));
api.MapPost("/internal-announcements",async(AnnouncementInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var text=input.Text?.Trim()??string.Empty;if(text.Length==0)return Results.BadRequest(new{error="กรุณากรอกเนื้อหาประกาศ"});
    return Results.Created("/api/internal-announcements",await repository.CreateInternalAnnouncementAsync(text,token));
});
api.MapPut("/internal-announcements/{id:int}",async(int id,AnnouncementInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var text=input.Text?.Trim()??string.Empty;if(text.Length==0)return Results.BadRequest(new{error="กรุณากรอกเนื้อหาประกาศ"});
    return await repository.UpdateInternalAnnouncementAsync(id,text,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบประกาศที่ต้องการแก้ไข"});
});
api.MapDelete("/internal-announcements/{id:int}",async(int id,LegacySqlRepository repository,CancellationToken token)=>await repository.DeleteInternalAnnouncementAsync(id,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบประกาศที่ต้องการลบ"}));
api.MapGet("/internal-announcement-images",async(LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetInternalImagesAsync(token)));
api.MapGet("/internal-announcement-images/{id}",async(string id,LegacySqlRepository repository,CancellationToken token)=>await repository.GetInternalImageAsync(id.Trim(),token) is { } image?Results.File(image.Data,image.ContentType):Results.NotFound());
api.MapPut("/internal-announcement-images/{id}",async(string id,InternalImageInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var comma=input.DataUrl?.IndexOf(',')??-1;if(comma<0)return Results.BadRequest(new{error="ข้อมูลรูปภาพไม่ถูกต้อง"});
    byte[] bytes;try{bytes=Convert.FromBase64String(input.DataUrl![(comma+1)..]);}catch{return Results.BadRequest(new{error="ข้อมูลรูปภาพไม่ถูกต้อง"});}
    if(bytes.Length is 0 or > 5_242_880)return Results.BadRequest(new{error="รูปภาพต้องมีขนาดไม่เกิน 5 MB"});
    var valid=bytes.Length>4&&((bytes[0]==0xFF&&bytes[1]==0xD8)||(bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4E&&bytes[3]==0x47)||(bytes[0]==0x47&&bytes[1]==0x49&&bytes[2]==0x46));
    if(!valid)return Results.BadRequest(new{error="รองรับเฉพาะไฟล์ JPG, PNG หรือ GIF"});
    return await repository.UpdateInternalImageAsync(id.Trim(),bytes,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบตำแหน่งประกาศรูปภาพ"});
});
api.MapDelete("/internal-announcement-images/{id}",async(string id,LegacySqlRepository repository,CancellationToken token)=>await repository.DeleteInternalImageAsync(id.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบตำแหน่งประกาศรูปภาพ"}));
api.MapGet("/website-entries",async(LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetWebsiteEntriesAsync(token)));
api.MapGet("/website-entries/{id}/image",async(string id,LegacySqlRepository repository,CancellationToken token)=>await repository.GetWebsiteEntryImageAsync(id.Trim(),token) is { } image?Results.File(image.Data,image.ContentType):Results.NotFound());
api.MapPost("/website-entries",async(WebsiteEntryInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var decoded=DecodeImage(input.DataUrl,true);if(decoded.Error is not null)return Results.BadRequest(new{error=decoded.Error});
    await repository.CreateWebsiteEntryAsync("1",input.Visible,decoded.Image!,token);return Results.Created("/api/website-entries/1",new WebsiteEntryRecord("1",input.Visible,true,"/api/website-entries/1/image"));
});
api.MapPut("/website-entries/{id}",async(string id,WebsiteEntryInput input,LegacySqlRepository repository,CancellationToken token)=>
{
    var decoded=DecodeImage(input.DataUrl,false);if(decoded.Error is not null)return Results.BadRequest(new{error=decoded.Error});
    return await repository.UpdateWebsiteEntryAsync(id.Trim(),input.Visible,decoded.Image,token)?Results.NoContent():Results.NotFound(new{error="ไม่พบข้อมูลหน้าเข้าเว็บไซต์"});
});
api.MapDelete("/website-entries/{id}",async(string id,LegacySqlRepository repository,CancellationToken token)=>await repository.DeleteWebsiteEntryAsync(id.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบข้อมูลหน้าเข้าเว็บไซต์"}));
api.MapGet("/customer-announcements",async(LegacySqlRepository repository,CancellationToken token)=>Results.Ok(await repository.GetCustomerAnnouncementsAsync(token)));
api.MapGet("/customer-announcements/{id:int}/image",async(int id,LegacySqlRepository repository,IConfiguration configuration,CancellationToken token)=>
{
    var row=(await repository.GetCustomerAnnouncementsAsync(token)).FirstOrDefault(item=>item.Id==id);if(row is null||!row.HasImage)return Results.NotFound();var file=Path.Combine(configuration["Api:LegacyCustomerImageRoot"]??string.Empty,Path.GetFileName(row.Path));return File.Exists(file)?Results.File(file,DetectFileContentType(file)):Results.NotFound();
});
api.MapPost("/customer-announcements",async(CustomerAnnouncementInput input,LegacySqlRepository repository,IConfiguration configuration,CancellationToken token)=>
{
    var error=ValidateCustomerAnnouncement(input,true);if(error is not null)return Results.BadRequest(new{error});var saved=SaveCustomerImage(input,configuration);if(saved.Error is not null)return Results.BadRequest(new{error=saved.Error});var id=await repository.CreateCustomerAnnouncementAsync(input.Name.Trim(),input.Type.Trim(),saved.Path??string.Empty,SafeCustomerUrl(input.Url),token);return Results.Created($"/api/customer-announcements/{id}",new{id});
});
api.MapPut("/customer-announcements/{id:int}",async(int id,CustomerAnnouncementInput input,LegacySqlRepository repository,IConfiguration configuration,CancellationToken token)=>
{
    var error=ValidateCustomerAnnouncement(input,false);if(error is not null)return Results.BadRequest(new{error});var saved=SaveCustomerImage(input,configuration);if(saved.Error is not null)return Results.BadRequest(new{error=saved.Error});return await repository.UpdateCustomerAnnouncementAsync(id,input.Name.Trim(),input.Type.Trim(),saved.Path,SafeCustomerUrl(input.Url),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบประกาศสำหรับลูกค้า"});
});
api.MapDelete("/customer-announcements/{id:int}",async(int id,LegacySqlRepository repository,IConfiguration configuration,CancellationToken token)=>
{
    var path=await repository.DeleteCustomerAnnouncementAsync(id,token);if(path is null)return Results.NotFound(new{error="ไม่พบประกาศสำหรับลูกค้า"});var file=Path.Combine(configuration["Api:LegacyCustomerImageRoot"]??string.Empty,Path.GetFileName(path));if(File.Exists(file))File.Delete(file);return Results.NoContent();
});
api.MapGet("/reports/insurance", async (string centerId, DateTime dueDate, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุ centerId" });
    return Results.Ok(await repository.GetInsuranceReportAsync(centerId.Trim(), dueDate, token));
});
api.MapGet("/reports/tax", async (string centerId, DateTime dueDate, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุ centerId" });
    return Results.Ok(await repository.GetTaxReportAsync(centerId.Trim(), dueDate, token));
});
api.MapGet("/notifications/tax", async (string centerId, DateTime dueDate, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุศูนย์บริการ" });
    return Results.Ok(await repository.GetTaxNotificationsAsync(centerId.Trim(), dueDate, token));
});
api.MapGet("/reports/ry", async (string centerId, DateTime startDate, DateTime endDate, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุ centerId" });
    if (startDate.Date > endDate.Date)return Results.BadRequest(new { error = "วันที่เริ่มต้นต้องไม่เกินวันที่สิ้นสุด" });
    return Results.Ok(await repository.GetRyReportAsync(centerId.Trim(), startDate, endDate, token));
});
api.MapGet("/customers", async (string? search, LegacySqlRepository repository, CancellationToken token) => Results.Ok(await repository.GetCustomersAsync(search ?? string.Empty, token)));
api.MapGet("/faults", async (string centerId, string? search, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(centerId))return Results.BadRequest(new { error = "ต้องระบุ centerId" });
    return Results.Ok(await repository.GetFaultsAsync(centerId.Trim(), search ?? string.Empty, token));
});
api.MapPost("/faults",async(FaultInput input,LegacySqlRepository repository,CancellationToken token)=>{if(ValidateFault(input) is { } error)return Results.BadRequest(new{error});var images=DecodeFaultImages(input);if(images.Error is not null)return Results.BadRequest(new{error=images.Error});var row=await repository.CreateFaultAsync(input,token);await repository.SaveFaultImagesAsync(row.Id,images.Image1,images.Image2,images.Image3,token);return Results.Created("/api/faults",row);});
api.MapPut("/faults/{id}",async(string id,FaultInput input,LegacySqlRepository repository,CancellationToken token)=>{if(ValidateFault(input) is { } error)return Results.BadRequest(new{error});var images=DecodeFaultImages(input);if(images.Error is not null)return Results.BadRequest(new{error=images.Error});if(!await repository.UpdateFaultAsync(id.Trim(),input,token))return Results.NotFound(new{error="ไม่พบรายการข้อผิดพลาด"});await repository.SaveFaultImagesAsync(id.Trim(),images.Image1,images.Image2,images.Image3,token);return Results.NoContent();});
api.MapDelete("/faults/{id}",async(string id,LegacySqlRepository repository,CancellationToken token)=>await repository.DeleteFaultAsync(id.Trim(),token)?Results.NoContent():Results.NotFound(new{error="ไม่พบรายการข้อผิดพลาด"}));
api.MapGet("/faults/{id}/images/{slot:int}",async(string id,int slot,LegacySqlRepository repository,CancellationToken token)=>slot is <1 or >3?Results.BadRequest():await repository.GetFaultImageAsync(id.Trim(),slot,token) is { } image?Results.File(image.Data,image.ContentType):Results.NotFound());
api.MapDelete("/faults/{id}/images/{slot:int}",async(string id,int slot,LegacySqlRepository repository,CancellationToken token)=>slot is <1 or >3?Results.BadRequest():await repository.DeleteFaultImageAsync(id.Trim(),slot,token)?Results.NoContent():Results.NotFound());
api.MapGet("/users/{userId}/menus", async (string userId, LegacySqlRepository repository, CancellationToken token) =>
{
    if (string.IsNullOrWhiteSpace(userId) || userId.Length > 5)return Results.BadRequest(new { error = "userId ไม่ถูกต้อง" });
    return Results.Ok(await repository.GetMenusAsync(userId.Trim(), token));
});

static string? ValidateCenter(CenterInput input)
{
    if (string.IsNullOrWhiteSpace(input.Name))return "กรุณาระบุชื่อศูนย์บริการ";
    if (string.IsNullOrWhiteSpace(input.Address))return "กรุณาระบุที่อยู่";
    if (string.IsNullOrWhiteSpace(input.Phone))return "กรุณาระบุเบอร์โทรศัพท์";
    if (input.Name.Trim().Length > 200 || input.Address.Trim().Length > 200 || input.Phone.Trim().Length > 200)return "ข้อมูลต้องไม่เกิน 200 ตัวอักษร";
    return null;
}

static string? ValidateInsurer(InsurerInput input)
{
    if (string.IsNullOrWhiteSpace(input.Name))return "กรุณาระบุชื่อบริษัทประกัน";
    if (input.Name.Trim().Length > 200)return "ชื่อบริษัทประกันต้องไม่เกิน 200 ตัวอักษร";
    if (input.Type.Trim().ToUpperInvariant() is not ("N" or "Y"))return "ประเภทบริษัทประกันต้องเป็น N หรือ Y";
    return null;
}

static string? ValidateVehicleType(VehicleTypeInput input)
{
    if (string.IsNullOrWhiteSpace(input.Name))return "กรุณาระบุชื่อประเภท รย.";
    if (input.Name.Trim().Length > 200)return "ชื่อประเภท รย. ต้องไม่เกิน 200 ตัวอักษร";
    return null;
}

static string? ValidateStaff(StaffInput input,bool creating)
{
    if(string.IsNullOrWhiteSpace(input.Name))return "กรุณาระบุชื่อพนักงาน";
    if(string.IsNullOrWhiteSpace(input.Address))return "กรุณาระบุที่อยู่";
    if(string.IsNullOrWhiteSpace(input.Phone))return "กรุณาระบุเบอร์โทร";
    if(string.IsNullOrWhiteSpace(input.CenterId))return "กรุณาเลือกศูนย์บริการ";
    if(input.UserType.Trim() is not ("User" or "Admin"))return "ประเภทพนักงานต้องเป็น User หรือ Admin";
    if(input.Name.Trim().Length>200||input.Address.Trim().Length>200||input.Phone.Trim().Length>200)return "ข้อมูลต้องไม่เกิน 200 ตัวอักษร";
    if(!string.IsNullOrEmpty(input.Password)&&input.Password.Length>200)return "รหัสผ่านต้องไม่เกิน 200 ตัวอักษร";
    return null;
}

static string? ValidatePermission(string userId,PermissionInput input)
{
    if(string.IsNullOrWhiteSpace(userId)||userId.Trim().Length>5)return "รหัสพนักงานไม่ถูกต้อง";
    if(input.ProgramIds is null)return "กรุณาระบุรายการสิทธิ์";
    if(input.ProgramIds.Any(id=>string.IsNullOrWhiteSpace(id)||id.Trim().Length>5))return "รหัสเมนูไม่ถูกต้อง";
    return null;
}

static (byte[]? Image,string? Error) DecodeImage(string? dataUrl,bool required)
{
    if(string.IsNullOrWhiteSpace(dataUrl))return required?(null,"กรุณาเลือกรูปภาพ"):(null,null);
    var comma=dataUrl.IndexOf(',');if(comma<0)return(null,"ข้อมูลรูปภาพไม่ถูกต้อง");
    byte[] bytes;try{bytes=Convert.FromBase64String(dataUrl[(comma+1)..]);}catch{return(null,"ข้อมูลรูปภาพไม่ถูกต้อง");}
    if(bytes.Length is 0 or > 5_242_880)return(null,"รูปภาพต้องมีขนาดไม่เกิน 5 MB");
    var valid=bytes.Length>4&&((bytes[0]==0xFF&&bytes[1]==0xD8)||(bytes[0]==0x89&&bytes[1]==0x50&&bytes[2]==0x4E&&bytes[3]==0x47)||(bytes[0]==0x47&&bytes[1]==0x49&&bytes[2]==0x46));
    return valid?(bytes,null):(null,"รองรับเฉพาะไฟล์ JPG, PNG หรือ GIF");
}

static string? ValidateCustomerAnnouncement(CustomerAnnouncementInput input,bool creating)
{
    if(string.IsNullOrWhiteSpace(input.Name))return "กรุณาระบุเรื่องประกาศ";if(input.Name.Trim().Length>100)return "เรื่องประกาศต้องไม่เกิน 100 ตัวอักษร";if(input.Type.Trim() is not ("I" or "T" or "IT"))return "ประเภทประกาศไม่ถูกต้อง";if((input.Type.Contains('I')&&creating)&&string.IsNullOrWhiteSpace(input.DataUrl))return "กรุณาเลือกรูปภาพ";return null;
}
static (string? Path,string? Error) SaveCustomerImage(CustomerAnnouncementInput input,IConfiguration configuration)
{
    if(string.IsNullOrWhiteSpace(input.DataUrl))return(null,null);var decoded=DecodeImage(input.DataUrl,false);if(decoded.Error is not null)return(null,decoded.Error);var root=configuration["Api:LegacyCustomerImageRoot"]??string.Empty;if(root.Length==0)return(null,"ยังไม่ได้กำหนดโฟลเดอร์รูปภาพระบบเดิม");Directory.CreateDirectory(root);var extension=decoded.Image![0]==0xFF?".jpg":decoded.Image[0]==0x89?".png":".gif";var fileName=$"customer-{Guid.NewGuid():N}{extension}";File.WriteAllBytes(Path.Combine(root,fileName),decoded.Image);return($"~/img/{fileName}",null);
}
static string SafeCustomerUrl(string? url)=>Uri.TryCreate(url?.Trim(),UriKind.Absolute,out var parsed)&&parsed.Scheme is "http" or "https"?parsed.ToString():string.Empty;
static string DetectFileContentType(string path)=>Path.GetExtension(path).ToLowerInvariant() switch{".png"=>"image/png",".gif"=>"image/gif",_=>"image/jpeg"};
static string? ValidateFault(FaultInput input){if(string.IsNullOrWhiteSpace(input.CustomerId)||input.CustomerId.Trim().Length>13)return "กรุณาระบุเลขบัตรประชาชนไม่เกิน 13 หลัก";if(string.IsNullOrWhiteSpace(input.CustomerName)||input.CustomerName.Trim().Length>150)return "กรุณาระบุชื่อลูกค้า";if(string.IsNullOrWhiteSpace(input.VehicleId)||input.VehicleId.Trim().Length>13)return "กรุณาระบุทะเบียนรถ";if(input.Description?.Trim().Length>200)return "หมายเหตุต้องไม่เกิน 200 ตัวอักษร";if(string.IsNullOrWhiteSpace(input.CenterId))return "กรุณาระบุศูนย์บริการ";return null;}
static string? ValidateJob(JobInput input){if(string.IsNullOrWhiteSpace(input.CustomerId))return "กรุณาระบุรหัสลูกค้า";if(string.IsNullOrWhiteSpace(input.LicensePlate))return "กรุณาระบุทะเบียนรถ";if(string.IsNullOrWhiteSpace(input.CenterId))return "กรุณาระบุศูนย์บริการ";if(input.Note?.Length>200)return "หมายเหตุต้องไม่เกิน 200 ตัวอักษร";return null;}
static (byte[]? Image1,byte[]? Image2,byte[]? Image3,string? Error) DecodeFaultImages(FaultInput input){var a=DecodeImage(input.Image1,false);if(a.Error is not null)return(null,null,null,a.Error);var b=DecodeImage(input.Image2,false);if(b.Error is not null)return(null,null,null,b.Error);var c=DecodeImage(input.Image3,false);return c.Error is null?(a.Image,b.Image,c.Image,null):(null,null,null,c.Error);}

static CookieOptions SessionCookieOptions(HttpContext context,DateTimeOffset? expires=null)
{
    var secure=context.Request.IsHttps;
    return new CookieOptions
    {
        HttpOnly=true,
        Secure=secure,
        SameSite=secure?SameSiteMode.None:SameSiteMode.Lax,
        IsEssential=true,
        Expires=expires,
        Path="/"
    };
}

app.Run();

public partial class Program;
