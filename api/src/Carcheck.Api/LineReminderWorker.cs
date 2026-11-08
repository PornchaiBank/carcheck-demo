namespace Carcheck.Api;

public sealed class LineReminderWorker(IServiceScopeFactory scopeFactory,IConfiguration configuration,ILogger<LineReminderWorker> logger):BackgroundService
{
    private DateOnly? lastRun;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try{await RunIfDueAsync(stoppingToken);}
            catch(Exception error){logger.LogError(error,"LINE automatic reminder failed");}
        }while(await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunIfDueAsync(CancellationToken token)
    {
        if(!configuration.GetValue<bool>("Line:AutomaticEnabled"))return;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()?"SE Asia Standard Time":"Asia/Bangkok");
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone);
        var day=DateOnly.FromDateTime(now.DateTime);
        var hour=configuration.GetValue("Line:SendHour",9);
        if(now.Hour<hour||lastRun==day)return;
        lastRun=day;
        var leadDays=Math.Clamp(configuration.GetValue("Line:LeadDays",30),0,365);
        using var scope=scopeFactory.CreateScope();
        var repository=scope.ServiceProvider.GetRequiredService<LegacySqlRepository>();
        var line=scope.ServiceProvider.GetRequiredService<LineMessagingClient>();
        if(!line.Configured){logger.LogWarning("LINE automatic reminder is enabled but credentials are missing");return;}
        var targets=await repository.GetAutomaticLineRemindersAsync(DateTime.Today.AddDays(leadDays),token);
        foreach(var item in targets)
        {
            if(!await repository.ReserveLineNotificationAsync(item.WorkId,item.CustomerId,item.DueDate,"TAX",token))continue;
            try
            {
                await line.PushAsync(item.LineUserId,LineMessagingClient.TaxMessage(new(item.WorkId,item.CustomerId,item.CustomerName,item.LicensePlate,item.Province,item.DueDate,item.CenterName)),token);
                await repository.CompleteLineNotificationAsync(item.WorkId,item.DueDate,"TAX",true,null,token);
            }
            catch(Exception error)
            {
                await repository.CompleteLineNotificationAsync(item.WorkId,item.DueDate,"TAX",false,error.Message,token);
                logger.LogWarning(error,"Could not send LINE reminder for work {WorkId}",item.WorkId);
            }
        }
    }
}
