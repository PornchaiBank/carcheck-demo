namespace Carcheck.Api;

public sealed record JobSummary(
    string WorkId,
    string CustomerId,
    string CustomerName,
    string CitizenId,
    string CustomerPhone,
    string CustomerAddress,
    string CustomerProvince,
    string CustomerAmphur,
    string CustomerDistrict,
    string CustomerZipcode,
    string LicensePlate,
    string Province,
    DateTime? ReceivedAt,
    DateTime? TaxExpiry,
    DateTime? InsuranceExpiry,
    decimal Total,
    string InsurerName,
    decimal InspectionFee,
    decimal Act1,
    decimal Act2,
    decimal Act3,
    decimal Act12,
    decimal OtherFee,
    decimal TaxFee,
    string Note,
    string CenterName,
    string Vin,
    string InsurerId,
    string VehicleTypeId);
public sealed record JobInput(string CustomerId,string LicensePlate,string Province,string InsurerId,decimal InspectionFee,decimal Act1,decimal Act2,decimal Act3,decimal Act12,decimal OtherFee,string VehicleTypeId,string CenterId,DateTime? TaxExpiry,DateTime? InsuranceExpiry,string? UserId,string? Note,string? Vin,decimal AnnualTax);

public sealed record LookupItem(string Id, string Name);
public sealed record AddressOption(string ProvinceCode,string ProvinceName,string AmphurCode,string AmphurName,string DistrictCode,string DistrictName,string Zipcode);
public sealed record CenterRecord(string Id, string Name, string Address, string Phone);
public sealed record CenterInput(string Name, string Address, string Phone);
public sealed record InsurerRecord(string Id, string Name, string Type);
public sealed record InsurerInput(string Name, string Type);
public sealed record VehicleTypeRecord(string Id, string Name);
public sealed record VehicleTypeInput(string Name);
public sealed record StaffRecord(string Id, string Name, string Address, string Phone, string CenterId, string CenterName, string UserType, bool Active);
public sealed record StaffInput(string Name, string? Password, string Address, string Phone, string CenterId, string UserType, bool Active, string? ModifiedBy);
public sealed record ProgramRecord(string Id, string Name);
public sealed record PermissionRecord(string UserId, string UserName, IReadOnlyList<string> ProgramIds, IReadOnlyList<string> ProgramNames);
public sealed record PermissionPage(IReadOnlyList<ProgramRecord> Programs, IReadOnlyList<PermissionRecord> Rows);
public sealed record PermissionInput(IReadOnlyList<string> ProgramIds);
public sealed record AnnouncementRecord(int Id, string Text);
public sealed record AnnouncementInput(string Text);
public sealed record InternalImageRecord(string Id, string Name, bool HasImage, string? Url);
public sealed record InternalImageInput(string DataUrl);
public sealed record WebsiteEntryRecord(string Id, bool Visible, bool HasImage, string? Url);
public sealed record WebsiteEntryInput(bool Visible, string? DataUrl);
public sealed record CustomerAnnouncementRecord(int Id, string Name, string Type, string Path, string Url, bool HasImage);
public sealed record CustomerAnnouncementInput(string Name, string Type, string? Url, string? DataUrl, string? FileName);
public sealed record CustomerSummary(string Id,string Name,string CitizenId,string Phone,string Address,string Province,string Amphur,string District,string Zipcode,int? AmphurCode,int? DistrictCode);
public sealed record FaultSummary(string Id, string CustomerId, string CustomerName, string VehicleId, string Description, DateTime? CreatedAt, string CenterId, bool HasImages);
public sealed record FaultInput(string CustomerId,string CustomerName,string VehicleId,string Description,string CenterId,string? UserId,string? Image1,string? Image2,string? Image3);
public sealed record MenuItem(string Id, string Name, string Url);
public sealed record DatabaseHealth(string Status, string Database, bool LegacySchemaFound, string Authentication);
public sealed record DatabaseSummary(long Centers, long Customers, long Jobs, long Faults, long Staff, long LineLinkedCustomers, long LineSent, long LineFailed);
public sealed record LineSettings(bool Automatic, int LeadDays, int SendHour, int SendMinute);
public sealed record LineSettingsInput(bool Automatic, int LeadDays, int SendHour, int SendMinute, string? UpdatedBy);
public sealed record PublicSlide(int Id, string Name, string Type, string Path, string Url);
public sealed record PublicImageInfo(string Id, string Name, string Url);
public sealed record PublicHomeContent(IReadOnlyList<string> Messages, IReadOnlyList<PublicSlide> Slides, bool HasHeaderImage, IReadOnlyList<PublicImageInfo> Announcements, IReadOnlyList<WebsiteEntryRecord> WebsiteEntries);
public sealed record BinaryAsset(byte[] Data, string ContentType);
public sealed record LoginRequest(string Username, string Password);
public sealed record LoginUser(string UserId, string DisplayName, string CenterId, string UserType, IReadOnlyList<string> ProgramIds);
public sealed record PasswordChangeInput(string UserId,string CurrentPassword,string NewPassword,string ConfirmPassword);
public sealed record AttendanceInput(string BridgeReadId,string PersonKey,string Name,string MaskedId,DateTimeOffset RecordedAt,string Day,string Method,string CenterId,string RecordedBy);
public sealed record AttendanceRecord(long Id,string BridgeReadId,string PersonKey,string Name,string MaskedId,DateTimeOffset RecordedAt,string Day,string Method,string CenterId,string RecordedBy);
public sealed record InsuranceReportRow(string CustomerName, string CustomerAddress, string LicensePlate, string Province, DateTime? DueDate, string CenterName, string CenterAddress);
public sealed record TaxNotificationRow(string WorkId, string CustomerId, string CustomerName, string CitizenId, string Phone, string LicensePlate, string Province, DateTime? DueDate, bool HasAct, bool HasInspection, string CenterName, bool LineLinked, DateTime? LineSentAt);
public sealed record LineLinkCodeInput(string CustomerId,string? CreatedBy);
public sealed record LineLinkCodeResult(string CustomerId,string Code,DateTimeOffset ExpiresAt);
public sealed record LineSendInput(string WorkId,string CustomerId,string CustomerName,string LicensePlate,string Province,DateTime DueDate,string CenterName);
public sealed record LineSendResult(bool Sent,string Status,string Message);
public sealed record LineReminderTarget(string WorkId,string CustomerId,string CustomerName,string LicensePlate,string Province,DateTime DueDate,string CenterName,string LineUserId);
public sealed record RyProvinceRow(string Province, int Count);
public sealed record RyReportResult(string CenterName, DateTime StartDate, DateTime EndDate, int Ry1, int Ry2, int Ry3, int Ry12, int WithAct, int WithoutAct, IReadOnlyList<RyProvinceRow> Provinces);

public static class Privacy
{
    public static string MaskCitizenId(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length < 4 ? "—" : $"x-xxxx-xxxxx-{digits[^3..]}-x";
    }
}
