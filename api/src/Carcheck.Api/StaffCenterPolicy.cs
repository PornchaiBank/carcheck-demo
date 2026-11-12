namespace Carcheck.Api;

public static class StaffCenterPolicy
{
    public static bool CanManageAllCenters(bool admin,string path,bool hasStaffPermission=false)=>(admin||hasStaffPermission)&&
        (path.Equals("/api/staff",StringComparison.OrdinalIgnoreCase)||path.StartsWith("/api/staff/",StringComparison.OrdinalIgnoreCase));
}
