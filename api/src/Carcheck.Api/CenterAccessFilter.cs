namespace Carcheck.Api;

public sealed class CenterAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next)
    {
        var http=context.HttpContext;
        var sessions=http.RequestServices.GetRequiredService<LoginSessionStore>();
        if(!sessions.TryGet(http.Request.Cookies["carcheck.session"],out var user)||user is null)return await next(context);
        var repository=http.RequestServices.GetRequiredService<LegacySqlRepository>();
        var access=await repository.GetStaffCenterAccessAsync(user.UserId,http.RequestAborted);
        http.Items["CenterAccess"]=access.Centers;
        var path=http.Request.Path.Value??string.Empty;
        var required=MenuAccessPolicy.RequiredPrograms(path,http.Request.Method);
        var programs=await repository.GetUserProgramIdsAsync(user.UserId,http.RequestAborted);
        if(!MenuAccessPolicy.IsAllowed(required,programs))return Results.Json(new{error="บัญชีนี้ไม่มีสิทธิ์ใช้งานเมนูนี้"},statusCode:403);
        // Staff administration must be able to assign centers beyond the
        // administrator's own operational data scope.
        if(StaffCenterPolicy.CanManageAllCenters(access.Admin,path,programs.Contains("004")))
        {
            http.Items["StaffManagement"]=true;
            return await next(context);
        }
        if(access.Centers.Length==0&&!path.StartsWith("/api/auth/",StringComparison.OrdinalIgnoreCase))return Results.Json(new{error="บัญชีนี้ไม่มีศูนย์บริการที่เข้าถึงได้"},statusCode:403);
        if(!access.Admin&&(path.StartsWith("/api/staff",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/api/centers",StringComparison.OrdinalIgnoreCase))&&!HttpMethods.IsGet(http.Request.Method))
            return Results.Json(new{error="เฉพาะผู้ดูแลระบบที่กำหนดข้อมูลและสิทธิ์ศูนย์บริการของพนักงานได้"},statusCode:403);
        var requested=new List<string>();
        if(http.Request.Query.TryGetValue("centerId",out var center))requested.Add(center.ToString());
        foreach(var argument in context.Arguments)
        {
            var property=argument?.GetType().GetProperty("CenterId");
            if(property?.GetValue(argument) is string id)requested.Add(id);
            if(argument is StaffInput staff&&staff.AllowedCenterIds is { } selected)requested.AddRange(selected);
            if(argument?.GetType().GetProperty("WorkId")?.GetValue(argument) is string workId)
            {
                var workCenter=await repository.GetResourceCenterAsync("jobs",workId,http.RequestAborted);
                if(!string.IsNullOrEmpty(workCenter))requested.Add(workCenter);
            }
        }
        var segments=path.Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(segments.Length>=3&&segments[1]=="centers")requested.Add(segments[2]);
        if(segments.Length>=3&&segments[1] is "vehicle-copies" or "faults")
        {
            var resourceCenter=await repository.GetResourceCenterAsync(segments[1],segments[2],http.RequestAborted);
            if(!string.IsNullOrEmpty(resourceCenter))requested.Add(resourceCenter);
        }
        if(requested.Any(id=>string.IsNullOrWhiteSpace(id)||!access.Centers.Contains(id.Trim(),StringComparer.OrdinalIgnoreCase)))
            return Results.Json(new{error="คุณไม่มีสิทธิ์เข้าถึงศูนย์บริการนี้"},statusCode:403);
        return await next(context);
    }
}
