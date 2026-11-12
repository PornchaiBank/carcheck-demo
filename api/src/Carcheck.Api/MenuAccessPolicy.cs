namespace Carcheck.Api;

public static class MenuAccessPolicy
{
    public static string[] RequiredPrograms(string path,string method)
    {
        var resource=path.Split('/',StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1)?.ToLowerInvariant();
        var read=method.Equals("GET",StringComparison.OrdinalIgnoreCase);
        return resource switch
        {
            "vehicle-copies"=>["019"],
            "attendance"=>["020"],
            "customers" when !method.Equals("GET",StringComparison.OrdinalIgnoreCase)=>["001"],
            "customers"=>["001","017"],
            // Jobs are also the data source for entry and daily reports.
            "jobs" when method.Equals("GET",StringComparison.OrdinalIgnoreCase)=>["001","008","018"],
            "jobs"=>["001"],
            "centers" when !read=>["003"],
            "staff"=>["004"],
            "permissions" or "users"=>["005"],
            "insurers"=>["006"],
            "vehicle-types"=>["007"],
            // Internal announcements are also displayed on the dashboard.
            "internal-announcements" or "internal-announcement-images" when !read=>["009"],
            "website-entries"=>["014"],
            "customer-announcements"=>["015"],
            "faults"=>["017"],
            "reports" when path.StartsWith("/api/reports/insurance",StringComparison.OrdinalIgnoreCase)=>["011"],
            "reports" when path.StartsWith("/api/reports/tax",StringComparison.OrdinalIgnoreCase)=>["012"],
            "reports" when path.StartsWith("/api/reports/ry",StringComparison.OrdinalIgnoreCase)=>["013"],
            "notifications"=>["016"],
            "line" when path.StartsWith("/api/line/customers/",StringComparison.OrdinalIgnoreCase)||path.Equals("/api/line/link-code",StringComparison.OrdinalIgnoreCase)=>["001","016"],
            "line" when !path.Equals("/api/line/webhook",StringComparison.OrdinalIgnoreCase)=>["016"],
            "account"=>["002"],
            _=>[]
        };
    }
    public static bool IsAllowed(IReadOnlyList<string> required,IReadOnlyList<string> assigned)=>required.Count==0||required.Any(id=>assigned.Any(value=>value.Trim().Equals(id,StringComparison.Ordinal)));
}
