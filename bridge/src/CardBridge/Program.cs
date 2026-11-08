using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

if (args.Contains("--self-test")) { Apdu.SelfTest(); AttendanceSession.SelfTest(); PairingToken.SelfTest(); return; }
if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("This reader requires Windows."); return; }
Console.OutputEncoding = Encoding.UTF8;
const string localOrigin = "http://127.0.0.1:17861";
bool allowWeb = args.Contains("--allow-carcheck-web");
using var instance = new Mutex(true,"Local\\CARCHECK.CardBridge.17861",out bool firstInstance);
if(!firstInstance){Console.Error.WriteLine("Close the existing Card Bridge before starting this version.");return;}
string token;
try { token=PairingToken.Load(args.Contains("--reset-pairing")); }
catch { Console.Error.WriteLine("Cannot read/save pairing settings. Run Reset-Pairing.cmd to create a new pairing code, then reconnect browsers."); return; }
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k => { k.Listen(IPAddress.Loopback,17861); k.Limits.MaxRequestBodySize=4096; });
var app=builder.Build();
var gate = new SemaphoreSlim(1,1);
var attendance=new AttendanceSession(SmartCard.Presence,SmartCard.Read);
app.Use(async (ctx,next) => {
    ctx.Response.Headers["Cache-Control"]="no-store";
    ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
    ctx.Response.Headers["Referrer-Policy"]="no-referrer";
    ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    if(ctx.Request.Host.Value != "127.0.0.1:17861") {ctx.Response.StatusCode=403;return;}
    string origin=ctx.Request.Headers.Origin.ToString();
    bool trusted=origin==localOrigin || (allowWeb && (origin=="https://pornchaibank.github.io" || origin=="http://127.0.0.1:5173"));
    if(origin.Length>0 && !trusted) {ctx.Response.StatusCode=403;return;}
    if(trusted) {
        ctx.Response.Headers["Access-Control-Allow-Origin"]=origin;
        ctx.Response.Headers["Vary"]="Origin";
        ctx.Response.Headers["Access-Control-Allow-Headers"]="Content-Type, X-Bridge-Token";
        ctx.Response.Headers["Access-Control-Allow-Methods"]="GET, POST, OPTIONS";
        ctx.Response.Headers["Access-Control-Allow-Private-Network"]="true";
    }
    if(ctx.Request.Method=="OPTIONS") {ctx.Response.StatusCode=trusted?204:403;return;}
    if(ctx.Request.Path.StartsWithSegments("/api")) {
        byte[] supplied=Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Bridge-Token"].ToString());
        if(!CryptographicOperations.FixedTimeEquals(supplied,Encoding.UTF8.GetBytes(token))) {ctx.Response.StatusCode=401;return;}
    }
    await next();
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/readers", () => {
    try { return Results.Ok(new { readers=SmartCard.Readers(), version=3 }); }
    catch(CardError e) {return Results.Json(new{error=e.Message},statusCode:503);}
});
app.MapPost("/api/read", async (ReadRequest input) => {
    if(!await gate.WaitAsync(0)) return Results.Json(new{error="เครื่องอ่านกำลังทำงาน กรุณารอสักครู่"},statusCode:409);
    try {
        if(string.IsNullOrWhiteSpace(input.Reader) || !SmartCard.Readers().Contains(input.Reader)) return Results.BadRequest(new{error="กรุณาเลือกเครื่องอ่านที่เชื่อมต่ออยู่"});
        return Results.Ok(await Task.Run(()=>SmartCard.Read(input.Reader)));
    } catch(CardError e) { return Results.Json(new{error=e.Message},statusCode:422); }
    catch { return Results.Json(new{error="เกิดข้อผิดพลาดภายในโปรแกรมอ่านบัตร"},statusCode:500); }
    finally {gate.Release();}
});
app.MapPost("/api/card", async (ReadRequest input) => {
    if(!await gate.WaitAsync(0)) return Results.Json(new{error="เครื่องอ่านกำลังทำงาน"},statusCode:409);
    try {
        if(string.IsNullOrWhiteSpace(input.Reader)) return Results.BadRequest(new{error="กรุณาเลือกเครื่องอ่าน"});
        return Results.Ok(await Task.Run(()=>attendance.Snapshot(input.Reader)));
    } catch(CardError e) { return Results.Json(new{error=e.Message},statusCode:422); }
    catch {return Results.Json(new{error="ตรวจสถานะเครื่องอ่านไม่สำเร็จ"},statusCode:500);}
    finally {gate.Release();}
});
app.MapPost("/api/attendance/confirm", async (ConfirmRequest input) => {
    if(!await gate.WaitAsync(0)) return Results.Json(new{error="เครื่องอ่านกำลังทำงาน"},statusCode:409);
    try {
        if(string.IsNullOrWhiteSpace(input.Reader)||string.IsNullOrWhiteSpace(input.ReadId)) return Results.BadRequest(new{error="กรุณาอ่านบัตรก่อน"});
        return Results.Ok(await Task.Run(()=>attendance.Confirm(input.Reader,input.ReadId)));
    } catch(CardError e) { return Results.Json(new{error=e.Message},statusCode:422); }
    catch {return Results.Json(new{error="ยืนยันการอ่านบัตรไม่สำเร็จ"},statusCode:500);}
    finally {gate.Release();}
});
await app.StartAsync();
Console.WriteLine("CARCHECK Card Bridge v3 - read only / no database / no card-data logs");
Console.WriteLine("Close this window or press Ctrl+C to stop.");
Console.WriteLine("Open this private local link (do not share it):");
string url=localOrigin+"/#"+token;
Console.WriteLine(url);
if(allowWeb) Console.WriteLine("Exact CARCHECK web origins are enabled (GitHub Pages and 127.0.0.1:5173). Pairing code is retained for this Windows user. Reset-Pairing.cmd revokes old codes.");
try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); } catch {Console.WriteLine("Open the local link manually.");}
await app.WaitForShutdownAsync();
record ReadRequest(string Reader);

record ConfirmRequest(string Reader,string ReadId);
