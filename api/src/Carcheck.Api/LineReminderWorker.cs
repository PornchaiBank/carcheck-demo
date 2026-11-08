namespace Carcheck.Api;

public sealed class LineReminderWorker(IServiceScopeFactory scopeFactory,ILogger<LineReminderWorker> logger):BackgroundService
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
        using var scope=scopeFactory.CreateScope();
        var repository=scope.ServiceProvider.GetRequiredService<LegacySqlRepository>();
        var settings=await repository.GetLineSettingsAsync(token);
        if(!settings.Automatic)return;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows()?"SE Asia Standard Time":"Asia/Bangkok");
        var now=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,zone);
        var day=DateOnly.FromDateTime(now.DateTime);
        if(now.Hour<settings.SendHour||(now.Hour==settings.SendHour&&now.Minute<settings.SendMinute)||lastRun==day)return;
        lastRun=day;
        var line=scope.ServiceProvider.GetRequiredService<LineMessagingClient>();
        if(!line.Configured){logger.LogWarning("LINE automatic reminder is enabled but credentials are missing");return;}
        var targets=await repository.GetAutomaticLineRemindersAsync(now.Date.AddDays(settings.LeadDays),token);
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
