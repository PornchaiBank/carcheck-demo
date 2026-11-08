using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Carcheck.Api;

public sealed class LegacySqlRepository(IConfiguration configuration)
{
    private readonly string? _connectionString = configuration.GetConnectionString("DefaultConnection");
    private readonly IConfiguration _configuration = configuration;

    public async Task EnsureAttendanceTableAsync(CancellationToken token)
    {
        const string sql="""
IF OBJECT_ID(N'dbo.db_attendance',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_attendance(
  id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_db_attendance PRIMARY KEY,
  bridge_read_id nvarchar(100) NOT NULL,
  person_key nvarchar(200) NOT NULL,
  employee_name nvarchar(250) NOT NULL,
  masked_citizen_id nvarchar(30) NOT NULL,
  recorded_at datetimeoffset(0) NOT NULL,
  work_day date NOT NULL,
  method nvarchar(50) NOT NULL,
  center_id varchar(200) NOT NULL,
  recorded_by varchar(200) NOT NULL,
  created_at datetime2(0) NOT NULL CONSTRAINT DF_db_attendance_created_at DEFAULT SYSDATETIME(),
  CONSTRAINT UQ_db_attendance_read UNIQUE(bridge_read_id),
  CONSTRAINT UQ_db_attendance_person_day UNIQUE(person_key,work_day)
 );
 CREATE INDEX IX_db_attendance_center_day ON dbo.db_attendance(center_id,work_day DESC,recorded_at DESC);
END
""";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection){CommandTimeout=30};await command.ExecuteNonQueryAsync(token);
    }

    public async Task EnsureLineTablesAsync(CancellationToken token)
    {
        const string sql="""
IF OBJECT_ID(N'dbo.db_line_customer',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_line_customer(
  id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_db_line_customer PRIMARY KEY,
  customer_id varchar(50) NOT NULL,
  line_user_id varchar(64) NOT NULL,
  display_name nvarchar(200) NULL,
  active bit NOT NULL CONSTRAINT DF_db_line_customer_active DEFAULT(1),
  linked_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_customer_linked DEFAULT(SYSDATETIME()),
  updated_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_customer_updated DEFAULT(SYSDATETIME()),
  CONSTRAINT UQ_db_line_customer_customer UNIQUE(customer_id),
  CONSTRAINT UQ_db_line_customer_user UNIQUE(line_user_id)
 );
END;
IF OBJECT_ID(N'dbo.db_line_link_code',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_line_link_code(
  code char(8) NOT NULL CONSTRAINT PK_db_line_link_code PRIMARY KEY,
  customer_id varchar(50) NOT NULL,
  expires_at datetime2(0) NOT NULL,
  used_at datetime2(0) NULL,
  created_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_link_code_created DEFAULT(SYSDATETIME()),
  created_by varchar(200) NOT NULL
 );
 CREATE INDEX IX_db_line_link_code_customer ON dbo.db_line_link_code(customer_id,expires_at DESC);
END;
IF OBJECT_ID(N'dbo.db_line_notification',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_line_notification(
  id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_db_line_notification PRIMARY KEY,
  work_id varchar(100) NOT NULL,
  customer_id varchar(50) NOT NULL,
  notification_type varchar(20) NOT NULL,
  due_date date NOT NULL,
  status varchar(20) NOT NULL,
  sent_at datetime2(0) NULL,
  error_message nvarchar(500) NULL,
  created_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_notification_created DEFAULT(SYSDATETIME()),
  updated_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_notification_updated DEFAULT(SYSDATETIME()),
  CONSTRAINT UQ_db_line_notification UNIQUE(work_id,notification_type,due_date)
 );
 CREATE INDEX IX_db_line_notification_status ON dbo.db_line_notification(status,due_date);
END;
IF OBJECT_ID(N'dbo.db_line_settings',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_line_settings(
  id tinyint NOT NULL CONSTRAINT PK_db_line_settings PRIMARY KEY,
  automatic bit NOT NULL,
  lead_days int NOT NULL,
  send_hour tinyint NOT NULL,
  send_minute tinyint NOT NULL,
  updated_by varchar(200) NOT NULL,
  updated_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_settings_updated DEFAULT(SYSDATETIME()),
  CONSTRAINT CK_db_line_settings_singleton CHECK(id=1),
  CONSTRAINT CK_db_line_settings_lead_days CHECK(lead_days BETWEEN 0 AND 365),
  CONSTRAINT CK_db_line_settings_hour CHECK(send_hour BETWEEN 0 AND 23),
  CONSTRAINT CK_db_line_settings_minute CHECK(send_minute BETWEEN 0 AND 59)
 );
END;
IF NOT EXISTS(SELECT 1 FROM dbo.db_line_settings WHERE id=1)
 INSERT dbo.db_line_settings(id,automatic,lead_days,send_hour,send_minute,updated_by) VALUES(1,@automatic,@leadDays,@sendHour,0,'SYSTEM');
IF OBJECT_ID(N'dbo.db_line_center_settings',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.db_line_center_settings(
  center_id varchar(200) NOT NULL CONSTRAINT PK_db_line_center_settings PRIMARY KEY,
  automatic bit NOT NULL,
  lead_days int NOT NULL,
  send_hour tinyint NOT NULL,
  send_minute tinyint NOT NULL,
  updated_by varchar(200) NOT NULL,
  updated_at datetime2(0) NOT NULL CONSTRAINT DF_db_line_center_settings_updated DEFAULT(SYSDATETIME()),
  CONSTRAINT CK_db_line_center_settings_lead_days CHECK(lead_days BETWEEN 0 AND 365),
  CONSTRAINT CK_db_line_center_settings_hour CHECK(send_hour BETWEEN 0 AND 23),
  CONSTRAINT CK_db_line_center_settings_minute CHECK(send_minute BETWEEN 0 AND 59)
 );
END;
""";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection){CommandTimeout=30};command.Parameters.Add("@automatic",SqlDbType.Bit).Value=_configuration.GetValue<bool>("Line:AutomaticEnabled");command.Parameters.Add("@leadDays",SqlDbType.Int).Value=Math.Clamp(_configuration.GetValue("Line:LeadDays",30),0,365);command.Parameters.Add("@sendHour",SqlDbType.TinyInt).Value=Math.Clamp(_configuration.GetValue("Line:SendHour",9),0,23);await command.ExecuteNonQueryAsync(token);
    }

    public async Task<LineSettings> GetLineSettingsAsync(string centerId,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT automatic,lead_days,send_hour,send_minute FROM dbo.db_line_center_settings WHERE center_id=@center",connection);command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=centerId;await using var reader=await command.ExecuteReaderAsync(CommandBehavior.SingleRow,token);if(!await reader.ReadAsync(token))return new(centerId,false,30,9,0);return new(centerId,Convert.ToBoolean(reader["automatic"]),Convert.ToInt32(reader["lead_days"]),Convert.ToInt32(reader["send_hour"]),Convert.ToInt32(reader["send_minute"]));}

    public async Task<IReadOnlyList<LineSettings>> GetAutomaticLineSettingsAsync(CancellationToken token){var rows=new List<LineSettings>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT center_id,automatic,lead_days,send_hour,send_minute FROM dbo.db_line_center_settings WHERE automatic=1",connection);await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))rows.Add(new(Text(reader,"center_id"),true,Convert.ToInt32(reader["lead_days"]),Convert.ToInt32(reader["send_hour"]),Convert.ToInt32(reader["send_minute"])));return rows;}

    public async Task<LineSettings> UpdateLineSettingsAsync(LineSettingsInput input,CancellationToken token){const string sql="MERGE dbo.db_line_center_settings AS target USING(SELECT @center center_id) source ON target.center_id=source.center_id WHEN MATCHED THEN UPDATE SET automatic=@automatic,lead_days=@leadDays,send_hour=@sendHour,send_minute=@sendMinute,updated_by=@updatedBy,updated_at=SYSDATETIME() WHEN NOT MATCHED THEN INSERT(center_id,automatic,lead_days,send_hour,send_minute,updated_by) VALUES(@center,@automatic,@leadDays,@sendHour,@sendMinute,@updatedBy);";await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=input.CenterId.Trim();command.Parameters.Add("@automatic",SqlDbType.Bit).Value=input.Automatic;command.Parameters.Add("@leadDays",SqlDbType.Int).Value=input.LeadDays;command.Parameters.Add("@sendHour",SqlDbType.TinyInt).Value=input.SendHour;command.Parameters.Add("@sendMinute",SqlDbType.TinyInt).Value=input.SendMinute;command.Parameters.Add("@updatedBy",SqlDbType.VarChar,200).Value=input.UpdatedBy?.Trim()??string.Empty;await command.ExecuteNonQueryAsync(token);return new(input.CenterId.Trim(),input.Automatic,input.LeadDays,input.SendHour,input.SendMinute);}

    public async Task<LineLinkCodeResult> CreateLineLinkCodeAsync(string customerId,string createdBy,CancellationToken token)
    {
        const string alphabet="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes=RandomNumberGenerator.GetBytes(8);var code=new string(bytes.Select(value=>alphabet[value%alphabet.Length]).ToArray());
        var expires=DateTimeOffset.Now.AddMinutes(15);
        const string sql="DELETE dbo.db_line_link_code WHERE customer_id=@customer AND used_at IS NULL; INSERT dbo.db_line_link_code(code,customer_id,expires_at,created_by) VALUES(@code,@customer,@expires,@user);";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@code",SqlDbType.Char,8).Value=code;command.Parameters.Add("@customer",SqlDbType.VarChar,50).Value=customerId;command.Parameters.Add("@expires",SqlDbType.DateTime2).Value=expires.LocalDateTime;command.Parameters.Add("@user",SqlDbType.VarChar,200).Value=createdBy;await command.ExecuteNonQueryAsync(token);return new(customerId,code,expires);
    }

    public async Task<string?> BindLineCustomerAsync(string code,string lineUserId,CancellationToken token)
    {
        const string sql="""
SET XACT_ABORT ON; BEGIN TRANSACTION;
DECLARE @customer varchar(50)=(SELECT customer_id FROM dbo.db_line_link_code WITH(UPDLOCK,HOLDLOCK) WHERE code=@code AND used_at IS NULL AND expires_at>SYSDATETIME());
IF @customer IS NULL BEGIN ROLLBACK; SELECT CAST(NULL AS varchar(50)); RETURN; END;
DELETE dbo.db_line_customer WHERE line_user_id=@lineUser AND customer_id<>@customer;
MERGE dbo.db_line_customer AS target USING(SELECT @customer customer_id) source ON target.customer_id=source.customer_id
WHEN MATCHED THEN UPDATE SET line_user_id=@lineUser,active=1,updated_at=SYSDATETIME()
WHEN NOT MATCHED THEN INSERT(customer_id,line_user_id) VALUES(@customer,@lineUser);
UPDATE dbo.db_line_link_code SET used_at=SYSDATETIME() WHERE code=@code;
COMMIT; SELECT @customer;
""";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@code",SqlDbType.Char,8).Value=code;command.Parameters.Add("@lineUser",SqlDbType.VarChar,64).Value=lineUserId;return Convert.ToString(await command.ExecuteScalarAsync(token)) is {Length:>0} customer?customer:null;
    }

    public async Task DeactivateLineUserAsync(string lineUserId,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_line_customer SET active=0,updated_at=SYSDATETIME() WHERE line_user_id=@lineUser",connection);command.Parameters.Add("@lineUser",SqlDbType.VarChar,64).Value=lineUserId;await command.ExecuteNonQueryAsync(token);}

    public async Task<bool> UnlinkLineCustomerAsync(string customerId,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_line_customer SET active=0,updated_at=SYSDATETIME() WHERE customer_id=@customer AND active=1",connection);command.Parameters.Add("@customer",SqlDbType.VarChar,50).Value=customerId;return await command.ExecuteNonQueryAsync(token)>0;}

    public async Task<string?> GetLineUserIdAsync(string customerId,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT line_user_id FROM dbo.db_line_customer WHERE customer_id=@customer AND active=1",connection);command.Parameters.Add("@customer",SqlDbType.VarChar,50).Value=customerId;return Convert.ToString(await command.ExecuteScalarAsync(token)) is {Length:>0} user?user:null;}

    public async Task<bool> ReserveLineNotificationAsync(string workId,string customerId,DateTime dueDate,string type,CancellationToken token)
    {
        const string sql="""
SET XACT_ABORT ON; BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.db_line_notification WITH(UPDLOCK,HOLDLOCK) WHERE work_id=@work AND notification_type=@type AND due_date=@due AND status='SENT') BEGIN ROLLBACK; SELECT CAST(0 AS bit); RETURN; END;
UPDATE dbo.db_line_notification SET customer_id=@customer,status='PENDING',error_message=NULL,updated_at=SYSDATETIME() WHERE work_id=@work AND notification_type=@type AND due_date=@due;
IF @@ROWCOUNT=0 INSERT dbo.db_line_notification(work_id,customer_id,notification_type,due_date,status) VALUES(@work,@customer,@type,@due,'PENDING');
COMMIT; SELECT CAST(1 AS bit);
""";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@work",SqlDbType.VarChar,100).Value=workId;command.Parameters.Add("@customer",SqlDbType.VarChar,50).Value=customerId;command.Parameters.Add("@type",SqlDbType.VarChar,20).Value=type;command.Parameters.Add("@due",SqlDbType.Date).Value=dueDate.Date;return Convert.ToBoolean(await command.ExecuteScalarAsync(token));
    }

    public async Task CompleteLineNotificationAsync(string workId,DateTime dueDate,string type,bool success,string? error,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_line_notification SET status=@status,sent_at=CASE WHEN @success=1 THEN SYSDATETIME() ELSE sent_at END,error_message=@error,updated_at=SYSDATETIME() WHERE work_id=@work AND notification_type=@type AND due_date=@due",connection);command.Parameters.Add("@status",SqlDbType.VarChar,20).Value=success?"SENT":"FAILED";command.Parameters.Add("@success",SqlDbType.Bit).Value=success;command.Parameters.Add("@error",SqlDbType.NVarChar,500).Value=(object?)error??DBNull.Value;command.Parameters.Add("@work",SqlDbType.VarChar,100).Value=workId;command.Parameters.Add("@type",SqlDbType.VarChar,20).Value=type;command.Parameters.Add("@due",SqlDbType.Date).Value=dueDate.Date;await command.ExecuteNonQueryAsync(token);}

    public async Task<IReadOnlyList<LineReminderTarget>> GetAutomaticLineRemindersAsync(string centerId,DateTime dueDate,CancellationToken token)
    {
        const string sql="""
SELECT w.work_id,w.Cus_id,w.Cus_name,w.license_plate,w.province,w.dete_extax,w.Name_center,l.line_user_id
FROM dbo.Vw_allworking w JOIN dbo.db_line_customer l ON l.customer_id=w.Cus_id COLLATE DATABASE_DEFAULT AND l.active=1
WHERE w.id_center=@center AND CAST(w.dete_extax AS date) BETWEEN CAST(GETDATE() AS date) AND @due AND (w.flag<>'1' OR w.flag IS NULL);
""";
        var result=new List<LineReminderTarget>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=centerId;command.Parameters.Add("@due",SqlDbType.Date).Value=dueDate.Date;await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))result.Add(new(Text(reader,"work_id"),Text(reader,"Cus_id"),Text(reader,"Cus_name"),Text(reader,"license_plate"),Text(reader,"province"),Date(reader,"dete_extax")!.Value,Text(reader,"Name_center"),Text(reader,"line_user_id")));return result;
    }

    public async Task<IReadOnlyList<AttendanceRecord>> GetAttendanceAsync(string centerId,CancellationToken token)
    {
        const string sql="SELECT TOP(200) id,bridge_read_id,person_key,employee_name,masked_citizen_id,recorded_at,work_day,method,center_id,recorded_by FROM dbo.db_attendance WITH(NOLOCK) WHERE center_id=@center ORDER BY recorded_at DESC,id DESC";
        var rows=new List<AttendanceRecord>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=centerId;await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))rows.Add(ReadAttendance(reader));return rows;
    }

    public async Task<AttendanceRecord> SaveAttendanceAsync(AttendanceInput input,CancellationToken token)
    {
        const string sql="""
BEGIN TRY
 INSERT dbo.db_attendance(bridge_read_id,person_key,employee_name,masked_citizen_id,recorded_at,work_day,method,center_id,recorded_by)
 VALUES(@read,@person,@name,@masked,@recorded,@day,@method,@center,@user);
END TRY BEGIN CATCH IF ERROR_NUMBER() NOT IN (2601,2627) THROW; END CATCH;
SELECT TOP(1) id,bridge_read_id,person_key,employee_name,masked_citizen_id,recorded_at,work_day,method,center_id,recorded_by
FROM dbo.db_attendance WHERE (person_key=@person AND work_day=@day) OR bridge_read_id=@read ORDER BY id DESC;
""";
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@read",SqlDbType.NVarChar,100).Value=input.BridgeReadId;command.Parameters.Add("@person",SqlDbType.NVarChar,200).Value=input.PersonKey;command.Parameters.Add("@name",SqlDbType.NVarChar,250).Value=input.Name;command.Parameters.Add("@masked",SqlDbType.NVarChar,30).Value=input.MaskedId;command.Parameters.Add("@recorded",SqlDbType.DateTimeOffset).Value=input.RecordedAt;command.Parameters.Add("@day",SqlDbType.Date).Value=DateOnly.Parse(input.Day);command.Parameters.Add("@method",SqlDbType.NVarChar,50).Value=input.Method;command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=input.CenterId;command.Parameters.Add("@user",SqlDbType.VarChar,200).Value=input.RecordedBy;await using var reader=await command.ExecuteReaderAsync(token);if(!await reader.ReadAsync(token))throw new InvalidOperationException("ไม่สามารถอ่านรายการลงเวลาที่บันทึกแล้ว");return ReadAttendance(reader);
    }

    private static AttendanceRecord ReadAttendance(SqlDataReader reader)=>new(Convert.ToInt64(reader["id"]),Text(reader,"bridge_read_id"),Text(reader,"person_key"),Text(reader,"employee_name"),Text(reader,"masked_citizen_id"),reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("recorded_at")),Convert.ToDateTime(reader["work_day"]).ToString("yyyy-MM-dd"),Text(reader,"method"),Text(reader,"center_id"),Text(reader,"recorded_by"));

    public async Task<LoginUser?> AuthenticateAsync(string username, string password, CancellationToken token)
    {
        const string sql = """
            SELECT User_id, User_name, User_password, Id_center, [type]
            FROM dbo.db_staff
            WHERE (flag <> '1' OR flag IS NULL)
              AND (User_id = @username OR User_name = @username);
            """;
        var expected = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        command.Parameters.Add("@username", SqlDbType.VarChar, 200).Value = username;
        LoginUser? authenticated = null;
        await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleResult, token))
        {
            while (await reader.ReadAsync(token))
            {
                var stored = Text(reader, "User_password").ToUpperInvariant();
                if (stored.Length != expected.Length)continue;
                if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(expected)))continue;
                authenticated = new LoginUser(Text(reader, "User_id"), Text(reader, "User_name"), Text(reader, "Id_center"), Text(reader, "type"), []);
                break;
            }
        }
        if (authenticated is null)return null;
        await using var permissionCommand = new SqlCommand("SELECT program_id FROM dbo.db_programlist WHERE user_id=@userId ORDER BY program_id", connection) { CommandTimeout = 10 };
        permissionCommand.Parameters.Add("@userId", SqlDbType.VarChar, 200).Value = authenticated.UserId;
        var programIds = new List<string>();
        await using var permissionReader = await permissionCommand.ExecuteReaderAsync(token);
        while (await permissionReader.ReadAsync(token))
        {
            var programId = Convert.ToString(permissionReader["program_id"])?.Trim();
            if (!string.IsNullOrEmpty(programId))programIds.Add(programId);
        }
        return authenticated with { ProgramIds = programIds };
    }

    public async Task<bool> ChangePasswordAsync(string userId,string currentPassword,string newPassword,CancellationToken token)
    {
        var currentHash=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(currentPassword)));var newHash=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(newPassword)));
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_staff SET User_password=@newPassword,Modify_date=CONVERT(varchar(10),GETDATE(),111) WHERE User_id=@id AND UPPER(User_password)=@currentPassword AND (flag<>'1' OR flag IS NULL) AND status='Y'",connection);command.Parameters.Add("@id",SqlDbType.VarChar,200).Value=userId.Trim();command.Parameters.Add("@currentPassword",SqlDbType.VarChar,200).Value=currentHash;command.Parameters.Add("@newPassword",SqlDbType.VarChar,200).Value=newHash;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<IReadOnlyList<JobSummary>> GetJobsAsync(string centerId, string? search, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (200)
                work_id, Cus_id, Cus_name, Cus_card, Cus_Tel,
                COALESCE(Cus_add, '') AS customer_address,
                COALESCE(Cus_province, '') AS customer_province,
                COALESCE(Cus_amphur, '') AS customer_amphur,
                COALESCE(Cus_district, '') AS customer_district,
                COALESCE(Cus_zipcode, '') AS customer_zipcode,
                license_plate, province, save_date,
                dete_extax, date_exinsurance,
                COALESCE(alone, 0) + COALESCE(act_1, 0) + COALESCE(act_2, 0) +
                COALESCE(act_3, 0) + COALESCE(act_4, 0) + COALESCE(other, 0) +
                COALESCE(vat, 0) AS total,
                name, alone, act_1, act_2, act_3, act_4, other, vat, note_data, Name_center, cc_car,
                COALESCE((SELECT TOP (1) id FROM dbo.db_insurer WHERE name=Vw_allworking.name), '') AS insurer_id,
                COALESCE((SELECT TOP (1) ID FROM dbo.db_typeact WHERE description=Vw_allworking.description), '') AS vehicle_type_id
            FROM dbo.Vw_allworking
            WHERE id_center = @centerId
              AND (flag <> '1' OR flag IS NULL)
              AND (@search = '' OR work_id LIKE @pattern OR license_plate LIKE @pattern OR Cus_name LIKE @pattern)
            ORDER BY save_date DESC, work_id DESC;
            """;

        var result = new List<JobSummary>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        command.Parameters.Add("@search", SqlDbType.NVarChar, 100).Value = search?.Trim() ?? string.Empty;
        command.Parameters.Add("@pattern", SqlDbType.NVarChar, 210).Value = $"%{EscapeLike(search?.Trim() ?? string.Empty)}%";
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new JobSummary(
                Text(reader, "work_id"),
                Text(reader, "Cus_id"),
                Text(reader, "Cus_name"),
                Text(reader, "Cus_card"),
                Text(reader, "Cus_Tel"),
                Text(reader, "customer_address"),
                Text(reader, "customer_province"),
                Text(reader, "customer_amphur"),
                Text(reader, "customer_district"),
                Text(reader, "customer_zipcode"),
                Text(reader, "license_plate"),
                Text(reader, "province"),
                Date(reader, "save_date"),
                Date(reader, "dete_extax"),
                Date(reader, "date_exinsurance"),
                Decimal(reader, "total"),
                Text(reader, "name"),
                Decimal(reader, "alone"),
                Decimal(reader, "act_1"),
                Decimal(reader, "act_2"),
                Decimal(reader, "act_3"),
                Decimal(reader, "act_4"),
                Decimal(reader, "other"),
                Decimal(reader, "vat"),
                Text(reader, "note_data"),
                Text(reader, "Name_center"),
                Text(reader, "cc_car"),
                Text(reader, "insurer_id"),
                Text(reader, "vehicle_type_id")));
        }
        return result;
    }

    public async Task<string> CreateJobAsync(JobInput input,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,token);var prefix=DateTime.Now.ToString("yyyyMMdd");await using var command=new SqlCommand("SELECT @prefix+RIGHT('000'+CONVERT(varchar(3),ISNULL(MAX(TRY_CONVERT(int,RIGHT(work_id,3))),0)+1),3) FROM dbo.db_working WITH(UPDLOCK,HOLDLOCK) WHERE LEFT(work_id,8)=@prefix AND id_center=@center",connection,transaction);command.Parameters.Add("@prefix",SqlDbType.VarChar,8).Value=prefix;command.Parameters.Add("@center",SqlDbType.VarChar,200).Value=input.CenterId;var id=Convert.ToString(await command.ExecuteScalarAsync(token))!;command.Parameters.Clear();command.CommandText="INSERT dbo.db_working(work_id,Cus_id,license_plate,province,insurer,alone,act_1,act_2,act_3,act_4,other,type_act,id_center,save_date,dete_extax,date_exinsurance,save_user,note_data,cc_car,vat,flag) VALUES(@id,@customer,@plate,@province,@insurer,@alone,@act1,@act2,@act3,@act12,@other,@type,@center,GETDATE(),@tax,@insurance,@user,@note,@vin,@vat,'0')";AddJobParameters(command,id,input);await command.ExecuteNonQueryAsync(token);await transaction.CommitAsync(token);return id;
    }
    public async Task<bool> UpdateJobAsync(string id,JobInput input,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_working SET Cus_id=@customer,license_plate=@plate,province=@province,insurer=@insurer,alone=@alone,act_1=@act1,act_2=@act2,act_3=@act3,act_4=@act12,other=@other,type_act=@type,dete_extax=@tax,date_exinsurance=@insurance,note_data=@note,cc_car=@vin,vat=@vat WHERE work_id=@id AND id_center=@center AND (flag<>'1' OR flag IS NULL)",connection);AddJobParameters(command,id,input);return await command.ExecuteNonQueryAsync(token)>0;}
    public async Task<bool> DeleteJobAsync(string id,string centerId,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_working SET flag='1' WHERE work_id=@id AND id_center=@center AND (flag<>'1' OR flag IS NULL)",connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@center",centerId);return await command.ExecuteNonQueryAsync(token)>0;}
    private static void AddJobParameters(SqlCommand command,string id,JobInput input){command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@customer",input.CustomerId);command.Parameters.AddWithValue("@plate",input.LicensePlate);command.Parameters.AddWithValue("@province",input.Province);command.Parameters.AddWithValue("@insurer",input.InsurerId);command.Parameters.AddWithValue("@alone",input.InspectionFee);command.Parameters.AddWithValue("@act1",input.Act1);command.Parameters.AddWithValue("@act2",input.Act2);command.Parameters.AddWithValue("@act3",input.Act3);command.Parameters.AddWithValue("@act12",input.Act12);command.Parameters.AddWithValue("@other",input.OtherFee);command.Parameters.AddWithValue("@type",input.VehicleTypeId);command.Parameters.AddWithValue("@center",input.CenterId);command.Parameters.Add("@tax",SqlDbType.DateTime).Value=(object?)input.TaxExpiry??DBNull.Value;command.Parameters.Add("@insurance",SqlDbType.DateTime).Value=(object?)input.InsuranceExpiry??DBNull.Value;command.Parameters.AddWithValue("@user",input.UserId??string.Empty);command.Parameters.AddWithValue("@note",input.Note??string.Empty);command.Parameters.AddWithValue("@vin",input.Vin??string.Empty);command.Parameters.AddWithValue("@vat",input.AnnualTax);}

    public Task<IReadOnlyList<LookupItem>> GetCentersAsync(CancellationToken token) =>
        LookupAsync("SELECT Id_center, Name_center FROM dbo.db_center WHERE flag = '0' ORDER BY Name_center", "Id_center", "Name_center", token);

    public async Task<IReadOnlyList<CenterRecord>> GetCenterRecordsAsync(CancellationToken token)
    {
        const string sql = "SELECT Id_center, Name_center, Add_center, Tel_center FROM dbo.db_center WHERE flag = '0' ORDER BY Id_center";
        var result = new List<CenterRecord>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new CenterRecord(Text(reader,"Id_center"),Text(reader,"Name_center"),Text(reader,"Add_center"),Text(reader,"Tel_center")));
        return result;
    }

    public async Task<CenterRecord> CreateCenterAsync(CenterInput input, CancellationToken token)
    {
        const string sql = """
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @next int = ISNULL((SELECT MAX(TRY_CONVERT(int, Id_center)) FROM dbo.db_center WITH (UPDLOCK, HOLDLOCK)), 0) + 1;
            DECLARE @id varchar(200) = CASE WHEN @next < 100 THEN RIGHT('00' + CONVERT(varchar(10), @next), 2) ELSE CONVERT(varchar(10), @next) END;
            INSERT dbo.db_center (Id_center, Name_center, Add_center, Tel_center, flag) VALUES (@id, @name, @address, @phone, '0');
            COMMIT TRANSACTION;
            SELECT @id AS Id_center;
            """;
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddCenterParameters(command, input);
        var id = Convert.ToString(await command.ExecuteScalarAsync(token)) ?? string.Empty;
        return new CenterRecord(id, input.Name.Trim(), input.Address.Trim(), input.Phone.Trim());
    }

    public async Task<bool> UpdateCenterAsync(string id, CenterInput input, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_center SET Name_center=@name, Add_center=@address, Tel_center=@phone WHERE Id_center=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddCenterParameters(command, input); command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task<bool> DeleteCenterAsync(string id, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_center SET flag='1' WHERE Id_center=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    private static void AddCenterParameters(SqlCommand command, CenterInput input)
    {
        command.Parameters.Add("@name", SqlDbType.VarChar, 200).Value = input.Name.Trim();
        command.Parameters.Add("@address", SqlDbType.VarChar, 200).Value = input.Address.Trim();
        command.Parameters.Add("@phone", SqlDbType.VarChar, 200).Value = input.Phone.Trim();
    }

    public async Task<IReadOnlyList<InsuranceReportRow>> GetInsuranceReportAsync(string centerId, DateTime dueDate, CancellationToken token)
    {
        const string sql = """
            SELECT Cus_name, Cus_address, license_plate, province, date_exinsurance, Name_center, Add_center
            FROM dbo.Vw_allworking
            WHERE id_center = @centerId
              AND CAST(date_exinsurance AS date) = @dueDate
              AND (flag <> '1' OR flag IS NULL)
            ORDER BY Cus_name, license_plate;
            """;
        var result = new List<InsuranceReportRow>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        command.Parameters.Add("@dueDate", SqlDbType.Date).Value = dueDate.Date;
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))result.Add(new InsuranceReportRow(Text(reader,"Cus_name"),Text(reader,"Cus_address"),Text(reader,"license_plate"),Text(reader,"province"),Date(reader,"date_exinsurance"),Text(reader,"Name_center"),Text(reader,"Add_center")));
        return result;
    }

    public async Task<IReadOnlyList<InsuranceReportRow>> GetTaxReportAsync(string centerId, DateTime dueDate, CancellationToken token)
    {
        const string sql = """
            SELECT Cus_name, Cus_address, license_plate, province, dete_extax, Name_center, Add_center
            FROM dbo.Vw_allworking
            WHERE id_center = @centerId
              AND CAST(dete_extax AS date) = @dueDate
              AND (flag <> '1' OR flag IS NULL)
            ORDER BY Cus_name, license_plate;
            """;
        var result = new List<InsuranceReportRow>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        command.Parameters.Add("@dueDate", SqlDbType.Date).Value = dueDate.Date;
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))result.Add(new InsuranceReportRow(Text(reader,"Cus_name"),Text(reader,"Cus_address"),Text(reader,"license_plate"),Text(reader,"province"),Date(reader,"dete_extax"),Text(reader,"Name_center"),Text(reader,"Add_center")));
        return result;
    }

    public async Task<IReadOnlyList<TaxNotificationRow>> GetTaxNotificationsAsync(string centerId, DateTime dueDate, CancellationToken token)
    {
        const string sql = """
            SELECT w.work_id, w.Cus_id, w.Cus_name, w.Cus_card, w.Cus_Tel, w.license_plate, w.province,
                w.dete_extax, w.alone, w.vat, w.Name_center,
                CAST(CASE WHEN l.customer_id IS NULL THEN 0 ELSE 1 END AS bit) line_linked,
                CASE WHEN l.customer_id IS NULL THEN NULL ELSE n.sent_at END line_sent_at
            FROM dbo.Vw_allworking w
            LEFT JOIN dbo.db_line_customer l ON l.customer_id=w.Cus_id COLLATE DATABASE_DEFAULT AND l.active=1
            LEFT JOIN dbo.db_line_notification n ON n.work_id=w.work_id COLLATE DATABASE_DEFAULT AND n.notification_type='TAX' AND n.due_date=CAST(w.dete_extax AS date) AND n.status='SENT'
            WHERE w.id_center = @centerId
              AND CAST(w.dete_extax AS date) = @dueDate
              AND (w.flag <> '1' OR w.flag IS NULL)
            ORDER BY w.Cus_name, w.license_plate;
            """;
        var result = new List<TaxNotificationRow>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        command.Parameters.Add("@dueDate", SqlDbType.Date).Value = dueDate.Date;
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))result.Add(new TaxNotificationRow(
            Text(reader,"work_id"),Text(reader,"Cus_id"),Text(reader,"Cus_name"),Text(reader,"Cus_card"),Text(reader,"Cus_Tel"),
            Text(reader,"license_plate"),Text(reader,"province"),Date(reader,"dete_extax"),
            Decimal(reader,"alone")>0,Decimal(reader,"vat")>0,Text(reader,"Name_center"),Convert.ToBoolean(reader["line_linked"]),Date(reader,"line_sent_at")));
        return result;
    }

    public async Task<RyReportResult> GetRyReportAsync(string centerId, DateTime startDate, DateTime endDate, CancellationToken token)
    {
        const string sql = """
            SELECT TOP (1) Name_center FROM dbo.db_center WHERE Id_center = @centerId;
            SELECT
                COALESCE(SUM(CASE WHEN type_act = '01' THEN 1 ELSE 0 END), 0) AS ry1,
                COALESCE(SUM(CASE WHEN type_act = '02' THEN 1 ELSE 0 END), 0) AS ry2,
                COALESCE(SUM(CASE WHEN type_act = '03' THEN 1 ELSE 0 END), 0) AS ry3,
                COALESCE(SUM(CASE WHEN type_act = '04' THEN 1 ELSE 0 END), 0) AS ry12,
                COALESCE(SUM(CASE WHEN NOT (act_1 = 0 AND act_2 = 0 AND act_3 = 0 AND act_4 = 0) THEN 1 ELSE 0 END), 0) AS with_act,
                COALESCE(SUM(CASE WHEN act_1 = 0 AND act_2 = 0 AND act_3 = 0 AND act_4 = 0 THEN 1 ELSE 0 END), 0) AS without_act
            FROM dbo.db_working
            WHERE id_center = @centerId AND save_date >= @startDate AND save_date < DATEADD(day, 1, @endDate)
              AND (flag = '0' OR flag IS NULL) AND type_act <> '' AND alone <> 0;
            SELECT COALESCE(NULLIF(LTRIM(RTRIM(province)), ''), N'ไม่ระบุจังหวัด') AS province, COUNT(*) AS num
            FROM dbo.db_working
            WHERE id_center = @centerId AND save_date >= @startDate AND save_date < DATEADD(day, 1, @endDate)
              AND (flag = '0' OR flag IS NULL) AND type_act <> ''
            GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(province)), ''), N'ไม่ระบุจังหวัด')
            ORDER BY province;
            """;
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        command.Parameters.Add("@startDate", SqlDbType.Date).Value = startDate.Date;
        command.Parameters.Add("@endDate", SqlDbType.Date).Value = endDate.Date;
        await using var reader = await command.ExecuteReaderAsync(token);
        var centerName = await reader.ReadAsync(token) ? Text(reader, "Name_center") : centerId;
        await reader.NextResultAsync(token);
        var ry1 = 0; var ry2 = 0; var ry3 = 0; var ry12 = 0; var withAct = 0; var withoutAct = 0;
        if (await reader.ReadAsync(token))
        {
            ry1 = Convert.ToInt32(reader["ry1"]); ry2 = Convert.ToInt32(reader["ry2"]); ry3 = Convert.ToInt32(reader["ry3"]); ry12 = Convert.ToInt32(reader["ry12"]);
            withAct = Convert.ToInt32(reader["with_act"]); withoutAct = Convert.ToInt32(reader["without_act"]);
        }
        await reader.NextResultAsync(token);
        var provinces = new List<RyProvinceRow>();
        while (await reader.ReadAsync(token)) provinces.Add(new RyProvinceRow(Text(reader, "province"), Convert.ToInt32(reader["num"])));
        return new RyReportResult(centerName, startDate.Date, endDate.Date, ry1, ry2, ry3, ry12, withAct, withoutAct, provinces);
    }

    public Task<IReadOnlyList<LookupItem>> GetInsurersAsync(CancellationToken token) =>
        LookupAsync("SELECT id, name FROM dbo.db_insurer WHERE flag = '0' ORDER BY name", "id", "name", token);

    public async Task<IReadOnlyList<InsurerRecord>> GetInsurerRecordsAsync(CancellationToken token)
    {
        const string sql = "SELECT id, name, type FROM dbo.db_insurer WHERE flag = '0' ORDER BY id";
        var result = new List<InsurerRecord>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new InsurerRecord(Text(reader,"id"),Text(reader,"name"),Text(reader,"type")));
        return result;
    }

    public async Task<InsurerRecord> CreateInsurerAsync(InsurerInput input, CancellationToken token)
    {
        const string sql = """
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @next int = ISNULL((SELECT MAX(TRY_CONVERT(int, id)) FROM dbo.db_insurer WITH (UPDLOCK, HOLDLOCK)), 0) + 1;
            DECLARE @id varchar(200) = CASE WHEN @next < 100 THEN RIGHT('00' + CONVERT(varchar(10), @next), 2) ELSE CONVERT(varchar(10), @next) END;
            INSERT dbo.db_insurer (id, name, type, flag) VALUES (@id, @name, @type, '0');
            COMMIT TRANSACTION;
            SELECT @id AS id;
            """;
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddInsurerParameters(command, input);
        var id = Convert.ToString(await command.ExecuteScalarAsync(token)) ?? string.Empty;
        return new InsurerRecord(id, input.Name.Trim(), input.Type.Trim().ToUpperInvariant());
    }

    public async Task<bool> UpdateInsurerAsync(string id, InsurerInput input, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_insurer SET name=@name, type=@type WHERE id=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddInsurerParameters(command, input); command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task<bool> DeleteInsurerAsync(string id, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_insurer SET flag='1' WHERE id=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    private static void AddInsurerParameters(SqlCommand command, InsurerInput input)
    {
        command.Parameters.Add("@name", SqlDbType.VarChar, 200).Value = input.Name.Trim();
        command.Parameters.Add("@type", SqlDbType.VarChar, 200).Value = input.Type.Trim().ToUpperInvariant();
    }

    public Task<IReadOnlyList<LookupItem>> GetVehicleTypesAsync(CancellationToken token) =>
        LookupAsync("SELECT id, description FROM dbo.db_typeact WHERE flag = '0' ORDER BY id", "id", "description", token);

    public async Task<IReadOnlyList<VehicleTypeRecord>> GetVehicleTypeRecordsAsync(CancellationToken token)
    {
        const string sql = "SELECT id, description FROM dbo.db_typeact WHERE flag = '0' ORDER BY id";
        var result = new List<VehicleTypeRecord>();
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new VehicleTypeRecord(Text(reader,"id"),Text(reader,"description")));
        return result;
    }

    public async Task<VehicleTypeRecord> CreateVehicleTypeAsync(VehicleTypeInput input, CancellationToken token)
    {
        const string sql = """
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @next int = ISNULL((SELECT MAX(TRY_CONVERT(int, id)) FROM dbo.db_typeact WITH (UPDLOCK, HOLDLOCK)), 0) + 1;
            DECLARE @id varchar(200) = CASE WHEN @next < 100 THEN RIGHT('00' + CONVERT(varchar(10), @next), 2) ELSE CONVERT(varchar(10), @next) END;
            INSERT dbo.db_typeact (id, description, flag) VALUES (@id, @name, '0');
            COMMIT TRANSACTION;
            SELECT @id AS id;
            """;
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@name", SqlDbType.VarChar, 200).Value = input.Name.Trim();
        var id = Convert.ToString(await command.ExecuteScalarAsync(token)) ?? string.Empty;
        return new VehicleTypeRecord(id, input.Name.Trim());
    }

    public async Task<bool> UpdateVehicleTypeAsync(string id, VehicleTypeInput input, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_typeact SET description=@name WHERE id=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@name", SqlDbType.VarChar, 200).Value = input.Name.Trim(); command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task<bool> DeleteVehicleTypeAsync(string id, CancellationToken token)
    {
        const string sql = "UPDATE dbo.db_typeact SET flag='1' WHERE id=@id AND flag='0'";
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@id", SqlDbType.VarChar, 200).Value = id;
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public Task<IReadOnlyList<LookupItem>> GetProvincesAsync(CancellationToken token) =>
        LookupAsync("SELECT id, name FROM dbo.db_province ORDER BY name", "id", "name", token);
    public async Task<IReadOnlyList<AddressOption>> GetAddressOptionsAsync(CancellationToken token){var result=new List<AddressOption>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT DISTINCT CONVERT(varchar(20),PROVINCE_CODE) province_code,PROVINCE_NAME,CONVERT(varchar(20),AMPHUR_CODE) amphur_code,AMPHUR_NAME,CONVERT(varchar(20),DISTRICT_CODE) district_code,DISTRICT_NAME,COALESCE(ZIPCODE,'') zipcode FROM dbo.PROVINCE ORDER BY PROVINCE_NAME,AMPHUR_NAME,DISTRICT_NAME",connection);await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token))result.Add(new AddressOption(Text(reader,"province_code"),Text(reader,"PROVINCE_NAME"),Text(reader,"amphur_code"),Text(reader,"AMPHUR_NAME"),Text(reader,"district_code"),Text(reader,"DISTRICT_NAME"),Text(reader,"zipcode")));return result;}

    public async Task<IReadOnlyList<StaffRecord>> GetStaffRecordsAsync(string viewerId, CancellationToken token)
    {
        const string sql = """
            SELECT s.User_id, s.User_name, s.User_add, s.User_tel, s.Id_center, c.Name_center, s.type, s.status
            FROM dbo.db_staff s LEFT JOIN dbo.db_center c ON c.Id_center=s.Id_center
            WHERE s.flag='0' AND (@viewerId='00001' OR s.User_id<>'00001')
            ORDER BY s.User_id;
            """;
        var result = new List<StaffRecord>();
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@viewerId", SqlDbType.VarChar, 200).Value = viewerId;
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) result.Add(new StaffRecord(Text(reader,"User_id"),Text(reader,"User_name"),Text(reader,"User_add"),Text(reader,"User_tel"),Text(reader,"Id_center"),Text(reader,"Name_center"),Text(reader,"type"),Text(reader,"status").Equals("Y",StringComparison.OrdinalIgnoreCase)));
        return result;
    }

    public async Task<StaffRecord> CreateStaffAsync(StaffInput input, CancellationToken token)
    {
        const string sql = """
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @next int = ISNULL((SELECT MAX(TRY_CONVERT(int, User_id)) FROM dbo.db_staff WITH (UPDLOCK, HOLDLOCK)), 0) + 1;
            DECLARE @id varchar(200) = RIGHT('00000' + CONVERT(varchar(10), @next), 5);
            INSERT dbo.db_staff(User_id,User_name,User_password,User_add,User_tel,Id_center,Create_date,Modify_date,name_id,type,status,flag)
            VALUES(@id,@name,@password,@address,@phone,@centerId,CONVERT(varchar(10),GETDATE(),111),CONVERT(varchar(10),GETDATE(),111),@modifiedBy,@userType,@status,'0');
            COMMIT TRANSACTION;
            SELECT @id AS id;
            """;
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddStaffParameters(command,input,true);
        var id=Convert.ToString(await command.ExecuteScalarAsync(token))??string.Empty;
        var centerName=await CenterNameAsync(connection,input.CenterId,token);
        return new StaffRecord(id,input.Name.Trim(),input.Address.Trim(),input.Phone.Trim(),input.CenterId.Trim(),centerName,input.UserType.Trim(),input.Active);
    }

    public async Task<bool> UpdateStaffAsync(string id, StaffInput input, CancellationToken token)
    {
        const string sql = """
            UPDATE dbo.db_staff SET User_name=@name,User_add=@address,User_tel=@phone,Id_center=@centerId,
                Modify_date=CONVERT(varchar(10),GETDATE(),111),name_id=@modifiedBy,type=@userType,status=@status,
                User_password=CASE WHEN @changePassword=1 THEN @password ELSE User_password END
            WHERE User_id=@id AND flag='0';
            """;
        await using var connection = new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        AddStaffParameters(command,input,false); command.Parameters.Add("@id",SqlDbType.VarChar,200).Value=id;
        return await command.ExecuteNonQueryAsync(token)>0;
    }

    public async Task<bool> DeleteStaffAsync(string id, CancellationToken token)
    {
        const string sql="UPDATE dbo.db_staff SET flag='1' WHERE User_id=@id AND flag='0'";
        await using var connection=new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command=new SqlCommand(sql,connection){CommandTimeout=15}; command.Parameters.Add("@id",SqlDbType.VarChar,200).Value=id;
        return await command.ExecuteNonQueryAsync(token)>0;
    }

    private static void AddStaffParameters(SqlCommand command, StaffInput input, bool creating)
    {
        var rawPassword=creating&&string.IsNullOrWhiteSpace(input.Password)?"1234":input.Password??string.Empty;
        var password=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(rawPassword)));
        command.Parameters.Add("@name",SqlDbType.VarChar,200).Value=input.Name.Trim();
        command.Parameters.Add("@password",SqlDbType.VarChar,200).Value=password;
        command.Parameters.Add("@changePassword",SqlDbType.Bit).Value=!string.IsNullOrWhiteSpace(input.Password);
        command.Parameters.Add("@address",SqlDbType.VarChar,200).Value=input.Address.Trim();
        command.Parameters.Add("@phone",SqlDbType.VarChar,200).Value=input.Phone.Trim();
        command.Parameters.Add("@centerId",SqlDbType.VarChar,200).Value=input.CenterId.Trim();
        command.Parameters.Add("@modifiedBy",SqlDbType.VarChar,200).Value=(input.ModifiedBy??string.Empty).Trim();
        command.Parameters.Add("@userType",SqlDbType.VarChar,200).Value=input.UserType.Trim();
        command.Parameters.Add("@status",SqlDbType.VarChar,2).Value=input.Active?"Y":"N";
    }

    private static async Task<string> CenterNameAsync(SqlConnection connection,string centerId,CancellationToken token)
    {
        await using var command=new SqlCommand("SELECT Name_center FROM dbo.db_center WHERE Id_center=@id",connection);
        command.Parameters.Add("@id",SqlDbType.VarChar,200).Value=centerId;
        return Convert.ToString(await command.ExecuteScalarAsync(token))??string.Empty;
    }

    public async Task<PermissionPage> GetPermissionsAsync(string viewerId,CancellationToken token)
    {
        const string sql="""
            SELECT ID,program_name FROM dbo.db_program ORDER BY TRY_CONVERT(int,important),ID;
            SELECT User_id,User_name FROM dbo.db_staff WHERE flag='0' AND (@viewerId='00001' OR User_id<>'00001') ORDER BY User_id;
            SELECT user_id,program_id FROM dbo.db_programlist ORDER BY user_id,program_id;
            """;
        var programs=new List<ProgramRecord>(); var staff=new List<(string Id,string Name)>(); var assigned=new Dictionary<string,List<string>>();
        await using var connection=new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command=new SqlCommand(sql,connection){CommandTimeout=15}; command.Parameters.Add("@viewerId",SqlDbType.VarChar,200).Value=viewerId;
        await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token))programs.Add(new ProgramRecord(Text(reader,"ID"),Text(reader,"program_name")));
        await reader.NextResultAsync(token); while(await reader.ReadAsync(token))staff.Add((Text(reader,"User_id"),Text(reader,"User_name")));
        await reader.NextResultAsync(token); while(await reader.ReadAsync(token)){var id=Text(reader,"user_id");if(!assigned.TryGetValue(id,out var list)){list=[];assigned[id]=list;}list.Add(Text(reader,"program_id"));}
        var names=programs.ToDictionary(item=>item.Id,item=>item.Name);
        var rows=staff.Select(item=>{var ids=assigned.GetValueOrDefault(item.Id)??[];return new PermissionRecord(item.Id,item.Name,ids,ids.Where(names.ContainsKey).Select(id=>names[id]).ToList());}).ToList();
        return new PermissionPage(programs,rows);
    }

    public async Task SavePermissionsAsync(string userId,IReadOnlyList<string> programIds,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(token);
        try
        {
            await using(var delete=new SqlCommand("DELETE dbo.db_programlist WHERE user_id=@userId",connection,transaction)){delete.Parameters.Add("@userId",SqlDbType.VarChar,5).Value=userId;await delete.ExecuteNonQueryAsync(token);}
            foreach(var programId in programIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await using var insert=new SqlCommand("INSERT dbo.db_programlist(user_id,program_id) SELECT @userId,@programId WHERE EXISTS(SELECT 1 FROM dbo.db_program WHERE ID=@programId)",connection,transaction);
                insert.Parameters.Add("@userId",SqlDbType.VarChar,5).Value=userId;insert.Parameters.Add("@programId",SqlDbType.VarChar,5).Value=programId;
                await insert.ExecuteNonQueryAsync(token);
            }
            await transaction.CommitAsync(token);
        }
        catch{await transaction.RollbackAsync(token);throw;}
    }

    public async Task<bool> DeletePermissionsAsync(string userId,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString()); await connection.OpenAsync(token);
        await using var command=new SqlCommand("DELETE dbo.db_programlist WHERE user_id=@userId",connection){CommandTimeout=15};command.Parameters.Add("@userId",SqlDbType.VarChar,5).Value=userId;
        return await command.ExecuteNonQueryAsync(token)>0;
    }

    public async Task<IReadOnlyList<AnnouncementRecord>> GetInternalAnnouncementsAsync(CancellationToken token)
    {
        var result=new List<AnnouncementRecord>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);
        await using var command=new SqlCommand("SELECT id,data FROM dbo.db_declare_text ORDER BY id",connection){CommandTimeout=15};await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token))result.Add(new AnnouncementRecord(Convert.ToInt32(reader["id"]),Text(reader,"data")));return result;
    }

    public async Task<AnnouncementRecord> CreateInternalAnnouncementAsync(string text,CancellationToken token)
    {
        const string sql="""
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;BEGIN TRANSACTION;
            DECLARE @id int=ISNULL((SELECT MAX(id) FROM dbo.db_declare_text WITH(UPDLOCK,HOLDLOCK)),0)+1;
            INSERT dbo.db_declare_text(id,data) VALUES(@id,@text);COMMIT TRANSACTION;SELECT @id;
            """;
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand(sql,connection){CommandTimeout=15};command.Parameters.Add("@text",SqlDbType.VarChar,-1).Value=text;
        return new AnnouncementRecord(Convert.ToInt32(await command.ExecuteScalarAsync(token)),text);
    }

    public async Task<bool> UpdateInternalAnnouncementAsync(int id,string text,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_declare_text SET data=@text WHERE id=@id",connection){CommandTimeout=15};command.Parameters.Add("@text",SqlDbType.VarChar,-1).Value=text;command.Parameters.Add("@id",SqlDbType.Int).Value=id;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<bool> DeleteInternalAnnouncementAsync(int id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("DELETE dbo.db_declare_text WHERE id=@id",connection){CommandTimeout=15};command.Parameters.Add("@id",SqlDbType.Int).Value=id;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<IReadOnlyList<InternalImageRecord>> GetInternalImagesAsync(CancellationToken token)
    {
        var result=new List<InternalImageRecord>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);
        await using var command=new SqlCommand("SELECT id,name,CASE WHEN image IS NULL OR DATALENGTH(image)=0 THEN 0 ELSE 1 END has_image FROM dbo.db_declare_image ORDER BY id",connection){CommandTimeout=15};await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token)){var id=Text(reader,"id");var has=Convert.ToBoolean(reader["has_image"]);result.Add(new InternalImageRecord(id,Text(reader,"name"),has,has?$"/api/internal-announcement-images/{Uri.EscapeDataString(id)}":null));}return result;
    }

    public async Task<BinaryAsset?> GetInternalImageAsync(string id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT image FROM dbo.db_declare_image WHERE id=@id",connection);command.Parameters.Add("@id",SqlDbType.VarChar,2).Value=id;var value=await command.ExecuteScalarAsync(token);return value is byte[] bytes&&bytes.Length>0?new BinaryAsset(bytes,DetectImageType(bytes)):null;
    }

    public async Task<bool> UpdateInternalImageAsync(string id,byte[] bytes,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_declare_image SET image=@image WHERE id=@id",connection){CommandTimeout=15};command.Parameters.Add("@image",SqlDbType.VarBinary,-1).Value=bytes;command.Parameters.Add("@id",SqlDbType.VarChar,2).Value=id;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<bool> DeleteInternalImageAsync(string id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_declare_image SET image=NULL WHERE id=@id",connection){CommandTimeout=15};command.Parameters.Add("@id",SqlDbType.VarChar,2).Value=id;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<IReadOnlyList<WebsiteEntryRecord>> GetWebsiteEntriesAsync(CancellationToken token)
    {
        var result=new List<WebsiteEntryRecord>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);
        await using var command=new SqlCommand("SELECT visitwebsite_id,visitwebsite_type,CASE WHEN visitwebsite_image IS NULL OR DATALENGTH(visitwebsite_image)=0 THEN 0 ELSE 1 END has_image FROM dbo.db_visitwebsite ORDER BY visitwebsite_id",connection){CommandTimeout=15};await using var reader=await command.ExecuteReaderAsync(token);
        while(await reader.ReadAsync(token)){var id=Text(reader,"visitwebsite_id");var has=Convert.ToBoolean(reader["has_image"]);result.Add(new WebsiteEntryRecord(id,Text(reader,"visitwebsite_type").Equals("Y",StringComparison.OrdinalIgnoreCase),has,has?$"/api/website-entries/{Uri.EscapeDataString(id)}/image":null));}return result;
    }

    public async Task<BinaryAsset?> GetWebsiteEntryImageAsync(string id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT visitwebsite_image FROM dbo.db_visitwebsite WHERE visitwebsite_id=@id",connection);command.Parameters.Add("@id",SqlDbType.VarChar,50).Value=id;var value=await command.ExecuteScalarAsync(token);return value is byte[] bytes&&bytes.Length>0?new BinaryAsset(bytes,DetectImageType(bytes)):null;
    }

    public async Task<bool> CreateWebsiteEntryAsync(string id,bool visible,byte[] image,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("INSERT dbo.db_visitwebsite(visitwebsite_id,visitwebsite_image,visitwebsite_type) VALUES(@id,@image,@type)",connection){CommandTimeout=15};command.Parameters.Add("@id",SqlDbType.VarChar,50).Value=id;command.Parameters.Add("@image",SqlDbType.VarBinary,-1).Value=image;command.Parameters.Add("@type",SqlDbType.VarChar,50).Value=visible?"Y":"N";return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<bool> UpdateWebsiteEntryAsync(string id,bool visible,byte[]? image,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);var sql=image is null?"UPDATE dbo.db_visitwebsite SET visitwebsite_type=@type WHERE visitwebsite_id=@id":"UPDATE dbo.db_visitwebsite SET visitwebsite_image=@image,visitwebsite_type=@type WHERE visitwebsite_id=@id";await using var command=new SqlCommand(sql,connection){CommandTimeout=15};command.Parameters.Add("@id",SqlDbType.VarChar,50).Value=id;command.Parameters.Add("@type",SqlDbType.VarChar,50).Value=visible?"Y":"N";if(image is not null)command.Parameters.Add("@image",SqlDbType.VarBinary,-1).Value=image;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<bool> DeleteWebsiteEntryAsync(string id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("DELETE dbo.db_visitwebsite WHERE visitwebsite_id=@id",connection){CommandTimeout=15};command.Parameters.Add("@id",SqlDbType.VarChar,50).Value=id;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<IReadOnlyList<CustomerAnnouncementRecord>> GetCustomerAnnouncementsAsync(CancellationToken token)
    {
        var result=new List<CustomerAnnouncementRecord>();await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("SELECT id,COALESCE(name,'') name,COALESCE(type,'') type,COALESCE(path,'') path,COALESCE(url,'') url FROM dbo.db_visitcustomer ORDER BY id",connection);await using var reader=await command.ExecuteReaderAsync(token);while(await reader.ReadAsync(token)){var path=Text(reader,"path");result.Add(new CustomerAnnouncementRecord(Convert.ToInt32(reader["id"]),Text(reader,"name"),Text(reader,"type"),path,SafeUrl(Text(reader,"url")),path.Length>0));}return result;
    }

    public async Task<int> CreateCustomerAnnouncementAsync(string name,string type,string path,string url,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("INSERT dbo.db_visitcustomer(name,type,path,url) OUTPUT INSERTED.id VALUES(@name,@type,@path,@url)",connection);command.Parameters.Add("@name",SqlDbType.VarChar,100).Value=name;command.Parameters.Add("@type",SqlDbType.VarChar,100).Value=type;command.Parameters.Add("@path",SqlDbType.VarChar,200).Value=path;command.Parameters.Add("@url",SqlDbType.VarChar,-1).Value=url;return Convert.ToInt32(await command.ExecuteScalarAsync(token));
    }

    public async Task<bool> UpdateCustomerAnnouncementAsync(int id,string name,string type,string? path,string url,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);var sql=path is null?"UPDATE dbo.db_visitcustomer SET name=@name,type=@type,url=@url WHERE id=@id":"UPDATE dbo.db_visitcustomer SET name=@name,type=@type,path=@path,url=@url WHERE id=@id";await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@id",SqlDbType.Int).Value=id;command.Parameters.Add("@name",SqlDbType.VarChar,100).Value=name;command.Parameters.Add("@type",SqlDbType.VarChar,100).Value=type;command.Parameters.Add("@url",SqlDbType.VarChar,-1).Value=url;if(path is not null)command.Parameters.Add("@path",SqlDbType.VarChar,200).Value=path;return await command.ExecuteNonQueryAsync(token)==1;
    }

    public async Task<string?> DeleteCustomerAnnouncementAsync(int id,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);await using var read=new SqlCommand("SELECT path FROM dbo.db_visitcustomer WHERE id=@id",connection,(SqlTransaction)transaction);read.Parameters.Add("@id",SqlDbType.Int).Value=id;var path=Convert.ToString(await read.ExecuteScalarAsync(token));if(path is null){await transaction.RollbackAsync(token);return null;}await using var delete=new SqlCommand("DELETE dbo.db_visitcustomer WHERE id=@id",connection,(SqlTransaction)transaction);delete.Parameters.Add("@id",SqlDbType.Int).Value=id;await delete.ExecuteNonQueryAsync(token);await transaction.CommitAsync(token);return path;
    }

    public async Task<DatabaseHealth> CheckConnectionAsync(CancellationToken token)
    {
        const string sql = """
            SELECT DB_NAME() AS database_name,
                CASE WHEN OBJECT_ID(N'dbo.db_working', N'U') IS NOT NULL
                      AND OBJECT_ID(N'dbo.Vw_allworking', N'V') IS NOT NULL
                     THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS schema_found;
            """;
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
        if (!await reader.ReadAsync(token))throw new InvalidOperationException("SQL Server ไม่คืนผลการตรวจสอบ");
        return new DatabaseHealth("ok", Text(reader, "database_name"), Convert.ToBoolean(reader["schema_found"]), "Windows");
    }

    public async Task<DatabaseSummary> GetSummaryAsync(string? centerId,CancellationToken token)
    {
        const string sql = """
            SELECT
                (SELECT COUNT_BIG(*) FROM dbo.db_center WITH (NOLOCK) WHERE flag <> '1' OR flag IS NULL) AS centers,
                (SELECT COUNT_BIG(DISTINCT Cus_id) FROM dbo.db_working WITH (NOLOCK) WHERE (flag <> '1' OR flag IS NULL) AND (@centerId='' OR id_center=@centerId)) AS customers,
                (SELECT COUNT_BIG(*) FROM dbo.db_working WITH (NOLOCK) WHERE (flag <> '1' OR flag IS NULL) AND (@centerId='' OR id_center=@centerId)) AS jobs,
                (SELECT COUNT_BIG(*) FROM dbo.db_fault_des WITH (NOLOCK) WHERE @centerId='' OR id_center=@centerId) AS faults,
                (SELECT COUNT_BIG(*) FROM dbo.db_staff WITH (NOLOCK) WHERE (flag <> '1' OR flag IS NULL) AND (@centerId='' OR Id_center=@centerId)) AS staff,
                (SELECT COUNT_BIG(*) FROM dbo.db_line_customer lc WITH (NOLOCK) WHERE lc.active=1 AND (@centerId='' OR EXISTS(SELECT 1 FROM dbo.db_working w WITH (NOLOCK) WHERE (w.flag<>'1' OR w.flag IS NULL) AND w.id_center=@centerId AND w.Cus_id COLLATE DATABASE_DEFAULT=lc.customer_id COLLATE DATABASE_DEFAULT))) AS line_linked_customers,
                (SELECT COUNT_BIG(*) FROM dbo.db_line_notification n WITH (NOLOCK) WHERE n.status='SENT' AND (@centerId='' OR EXISTS(SELECT 1 FROM dbo.Vw_allworking w WHERE w.id_center=@centerId AND w.work_id COLLATE DATABASE_DEFAULT=n.work_id COLLATE DATABASE_DEFAULT))) AS line_sent,
                (SELECT COUNT_BIG(*) FROM dbo.db_line_notification n WITH (NOLOCK) WHERE n.status='FAILED' AND (@centerId='' OR EXISTS(SELECT 1 FROM dbo.Vw_allworking w WHERE w.id_center=@centerId AND w.work_id COLLATE DATABASE_DEFAULT=n.work_id COLLATE DATABASE_DEFAULT))) AS line_failed;
            """;
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@centerId",SqlDbType.VarChar,200).Value=centerId??string.Empty;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
        if (!await reader.ReadAsync(token))throw new InvalidOperationException("SQL Server ไม่คืนข้อมูลสรุป");
        return new DatabaseSummary(Convert.ToInt64(reader["centers"]), Convert.ToInt64(reader["customers"]), Convert.ToInt64(reader["jobs"]), Convert.ToInt64(reader["faults"]), Convert.ToInt64(reader["staff"]), Convert.ToInt64(reader["line_linked_customers"]), Convert.ToInt64(reader["line_sent"]), Convert.ToInt64(reader["line_failed"]));
    }

    public async Task<PublicHomeContent> GetPublicHomeAsync(CancellationToken token)
    {
        var messages = new List<string>();
        var slides = new List<PublicSlide>();
        var announcements = new List<PublicImageInfo>();
        var websiteEntries = new List<WebsiteEntryRecord>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using (var command = new SqlCommand("SELECT data FROM dbo.db_declare_text WHERE NULLIF(LTRIM(RTRIM(data)), '') IS NOT NULL ORDER BY id", connection))
        await using (var reader = await command.ExecuteReaderAsync(token))while (await reader.ReadAsync(token))messages.Add(Convert.ToString(reader[0])?.Trim() ?? string.Empty);
        await using (var command = new SqlCommand("SELECT id, name, type, path, url FROM dbo.db_visitcustomer ORDER BY id", connection))
        await using (var reader = await command.ExecuteReaderAsync(token))while (await reader.ReadAsync(token)){var id=Convert.ToInt32(reader["id"]);var name=Text(reader,"name");var type=Text(reader,"type");var path=Text(reader,"path");slides.Add(new PublicSlide(id,name,type,path,SafeUrl(Text(reader,"url"))));if(type.Contains('I',StringComparison.OrdinalIgnoreCase)&&path.Length>0)announcements.Add(new PublicImageInfo(id.ToString(),name,$"/api/public/customer-announcements/{id}"));}
        await using (var command = new SqlCommand("SELECT visitwebsite_id,visitwebsite_type,CASE WHEN visitwebsite_image IS NULL OR DATALENGTH(visitwebsite_image)=0 THEN 0 ELSE 1 END has_image FROM dbo.db_visitwebsite ORDER BY visitwebsite_id",connection))
        await using (var reader = await command.ExecuteReaderAsync(token))while(await reader.ReadAsync(token)){var id=Text(reader,"visitwebsite_id");var has=Convert.ToBoolean(reader["has_image"]);websiteEntries.Add(new WebsiteEntryRecord(id,Text(reader,"visitwebsite_type").Equals("Y",StringComparison.OrdinalIgnoreCase),has,has?$"/api/public/website-entry/{Uri.EscapeDataString(id)}":null));}
        await using var headerCommand = new SqlCommand("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.db_visitcustomer_head WHERE id='1' AND image_head IS NOT NULL) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END", connection);
        var hasHeader = Convert.ToBoolean(await headerCommand.ExecuteScalarAsync(token));
        return new PublicHomeContent(messages, slides, hasHeader, announcements, websiteEntries);
    }

    public Task<BinaryAsset?> GetHeaderImageAsync(CancellationToken token) => GetBinaryAsync("SELECT image_head FROM dbo.db_visitcustomer_head WHERE id='1'", [], token);
    public Task<BinaryAsset?> GetAnnouncementImageAsync(string id, CancellationToken token) => GetBinaryAsync("SELECT image FROM dbo.db_declare_image WHERE id=@id", [new SqlParameter("@id", SqlDbType.VarChar, 2){Value=id}], token);

    private async Task<BinaryAsset?> GetBinaryAsync(string sql, IReadOnlyList<SqlParameter> parameters, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        foreach(var parameter in parameters)command.Parameters.Add(parameter);
        var value=await command.ExecuteScalarAsync(token);
        if(value is not byte[] data || data.Length==0)return null;
        if(data.Length>10*1024*1024)throw new InvalidOperationException("รูปมีขนาดเกิน 10 MB");
        return new BinaryAsset(data,DetectImageType(data));
    }

    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(string search, CancellationToken token)
    {
        const string sql = """
            SELECT TOP (100) Cus_id, Cus_name, Cus_card, Cus_Tel,
                COALESCE(Cus_add, '') AS address,
                COALESCE(Cus_province, '') AS province,
                COALESCE(Cus_amphur, '') AS amphur,
                COALESCE(Cus_district, '') AS district,
                COALESCE(Cus_zipcode, '') AS zipcode,
                Cus_amphur_code, Cus_district_code
            FROM dbo.db_customer WITH (NOLOCK)
            WHERE (flag <> '1' OR flag IS NULL)
              AND (@search = '' OR Cus_id LIKE @pattern OR Cus_name LIKE @pattern OR Cus_card LIKE @pattern OR Cus_Tel LIKE @pattern)
            ORDER BY Cus_id
            OPTION (RECOMPILE);
            """;
        var result = new List<CustomerSummary>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        AddSearch(command, search);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))result.Add(new CustomerSummary(Text(reader,"Cus_id"),Text(reader,"Cus_name"),Text(reader,"Cus_card"),Text(reader,"Cus_Tel"),Text(reader,"address"),Text(reader,"province"),Text(reader,"amphur"),Text(reader,"district"),Text(reader,"zipcode"),reader["Cus_amphur_code"] is DBNull?null:Convert.ToInt32(reader["Cus_amphur_code"]),reader["Cus_district_code"] is DBNull?null:Convert.ToInt32(reader["Cus_district_code"])));
        return result;
    }

    public async Task<IReadOnlyList<FaultSummary>> GetFaultsAsync(string centerId, string search, CancellationToken token)
    {
        const string sql = """
            SELECT TOP (100) d.id, d.id_cus,
                LTRIM(RTRIM(CONCAT(COALESCE(d.name_cus, ''), ' ', COALESCE(d.lastname_cus, '')))) AS customer_name,
                d.id_car, d.mark, d.date_create, d.id_center,
                CASE WHEN f.img1 IS NOT NULL OR f.img2 IS NOT NULL OR f.img3 IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS has_images
            FROM dbo.db_fault_des AS d
            LEFT JOIN dbo.db_fault AS f ON f.id = d.id
            WHERE d.id_center = @centerId
              AND (@search = '' OR d.id LIKE @pattern OR d.id_cus LIKE @pattern OR d.id_car LIKE @pattern OR d.name_cus LIKE @pattern)
            ORDER BY d.date_create DESC, d.id DESC;
            """;
        var result = new List<FaultSummary>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@centerId", SqlDbType.NVarChar, 50).Value = centerId;
        AddSearch(command, search);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))result.Add(new FaultSummary(Text(reader, "id"), Text(reader, "id_cus"), Text(reader, "customer_name"), Text(reader, "id_car"), Text(reader, "mark"), Date(reader, "date_create"), Text(reader, "id_center"), Convert.ToBoolean(reader["has_images"])));
        return result;
    }

    public async Task<FaultSummary> CreateFaultAsync(FaultInput input,CancellationToken token)
    {
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable,token);var year=DateTime.Now.ToString("yy");await using var command=new SqlCommand("SELECT @year+RIGHT('0000000'+CONVERT(varchar(7),ISNULL(MAX(TRY_CONVERT(int,SUBSTRING(id,3,7))),0)+1),7) FROM dbo.db_fault_des WITH(UPDLOCK,HOLDLOCK) WHERE LEFT(id,2)=@year",connection,transaction);command.Parameters.Add("@year",SqlDbType.VarChar,2).Value=year;var id=Convert.ToString(await command.ExecuteScalarAsync(token))!;command.CommandText="INSERT dbo.db_fault_des(id,id_cus,name_cus,id_car,mark,date_create,user_create,id_center) VALUES(@id,@customerId,@name,@vehicle,@description,SYSDATETIME(),@user,@center)";command.Parameters.Add("@id",SqlDbType.NVarChar,13).Value=id;command.Parameters.Add("@customerId",SqlDbType.NVarChar,13).Value=input.CustomerId;command.Parameters.Add("@name",SqlDbType.NVarChar,150).Value=input.CustomerName;command.Parameters.Add("@vehicle",SqlDbType.NVarChar,13).Value=input.VehicleId;command.Parameters.Add("@description",SqlDbType.NVarChar,200).Value=input.Description;command.Parameters.Add("@user",SqlDbType.NVarChar,50).Value=input.UserId??string.Empty;command.Parameters.Add("@center",SqlDbType.NVarChar,50).Value=input.CenterId;await command.ExecuteNonQueryAsync(token);await transaction.CommitAsync(token);return new FaultSummary(id,input.CustomerId,input.CustomerName,input.VehicleId,input.Description,DateTime.Now,input.CenterId,false);
    }
    public async Task<bool> UpdateFaultAsync(string id,FaultInput input,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand("UPDATE dbo.db_fault_des SET id_cus=@customerId,name_cus=@name,id_car=@vehicle,mark=@description,date_modifile=SYSDATETIME(),user_modifile=@user,id_center=@center WHERE id=@id",connection);command.Parameters.AddWithValue("@id",id);command.Parameters.AddWithValue("@customerId",input.CustomerId);command.Parameters.AddWithValue("@name",input.CustomerName);command.Parameters.AddWithValue("@vehicle",input.VehicleId);command.Parameters.AddWithValue("@description",input.Description);command.Parameters.AddWithValue("@user",input.UserId??string.Empty);command.Parameters.AddWithValue("@center",input.CenterId);return await command.ExecuteNonQueryAsync(token)>0;}
    public async Task<bool> DeleteFaultAsync(string id,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var transaction=await connection.BeginTransactionAsync(token);await using var images=new SqlCommand("DELETE dbo.db_fault WHERE id=@id",connection,(SqlTransaction)transaction);images.Parameters.AddWithValue("@id",id);await images.ExecuteNonQueryAsync(token);await using var detail=new SqlCommand("DELETE dbo.db_fault_des WHERE id=@id",connection,(SqlTransaction)transaction);detail.Parameters.AddWithValue("@id",id);var count=await detail.ExecuteNonQueryAsync(token);await transaction.CommitAsync(token);return count>0;}
    public async Task SaveFaultImagesAsync(string id,byte[]? image1,byte[]? image2,byte[]? image3,CancellationToken token)
    {
        if(image1 is null&&image2 is null&&image3 is null)return;
        await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);
        const string sql = """
IF EXISTS(SELECT 1 FROM dbo.db_fault WHERE id=@id)
 UPDATE dbo.db_fault SET img1=COALESCE(@img1,img1),img2=COALESCE(@img2,img2),img3=COALESCE(@img3,img3) WHERE id=@id;
ELSE INSERT dbo.db_fault(id,img1,img2,img3) VALUES(@id,@img1,@img2,@img3);
""";await using var command=new SqlCommand(sql,connection);command.Parameters.Add("@id",SqlDbType.NVarChar,13).Value=id;command.Parameters.Add("@img1",SqlDbType.VarBinary,-1).Value=(object?)image1??DBNull.Value;command.Parameters.Add("@img2",SqlDbType.VarBinary,-1).Value=(object?)image2??DBNull.Value;command.Parameters.Add("@img3",SqlDbType.VarBinary,-1).Value=(object?)image3??DBNull.Value;await command.ExecuteNonQueryAsync(token);
    }
    public Task<BinaryAsset?> GetFaultImageAsync(string id,int slot,CancellationToken token)=>GetBinaryAsync($"SELECT img{slot} FROM dbo.db_fault WHERE id=@id",[new SqlParameter("@id",SqlDbType.NVarChar,13){Value=id}],token);
    public async Task<bool> DeleteFaultImageAsync(string id,int slot,CancellationToken token){await using var connection=new SqlConnection(ConnectionString());await connection.OpenAsync(token);await using var command=new SqlCommand($"UPDATE dbo.db_fault SET img{slot}=NULL WHERE id=@id",connection);command.Parameters.Add("@id",SqlDbType.NVarChar,13).Value=id;return await command.ExecuteNonQueryAsync(token)>0;}

    public async Task<IReadOnlyList<MenuItem>> GetMenusAsync(string userId, CancellationToken token)
    {
        const string sql = """
            SELECT p.ID, p.program_name, p.program_url
            FROM dbo.db_programlist AS pl
            INNER JOIN dbo.db_program AS p ON p.ID = pl.program_id
            WHERE pl.user_id = @userId
            ORDER BY p.important, p.ID;
            """;
        var result = new List<MenuItem>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@userId", SqlDbType.VarChar, 5).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))result.Add(new MenuItem(Text(reader, "ID"), Text(reader, "program_name"), Text(reader, "program_url")));
        return result;
    }

    private async Task<IReadOnlyList<LookupItem>> LookupAsync(string sql, string idColumn, string nameColumn, CancellationToken token)
    {
        var result = new List<LookupItem>();
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, token);
        while (await reader.ReadAsync(token))result.Add(new LookupItem(Text(reader, idColumn), Text(reader, nameColumn)));
        return result;
    }

    private static string Text(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? string.Empty : Convert.ToString(value)?.Trim() ?? string.Empty;
    }
    private static DateTime? Date(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? null : Convert.ToDateTime(value);
    }
    private static decimal Decimal(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? 0 : Convert.ToDecimal(value);
    }
    private static string EscapeLike(string value) => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
    private static void AddSearch(SqlCommand command, string? search)
    {
        var value = search?.Trim() ?? string.Empty;
        command.Parameters.Add("@search", SqlDbType.NVarChar, 100).Value = value;
        command.Parameters.Add("@pattern", SqlDbType.NVarChar, 210).Value = $"%{EscapeLike(value)}%";
    }
    private static string SafeUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme==Uri.UriSchemeHttp || uri.Scheme==Uri.UriSchemeHttps) ? uri.ToString() : string.Empty;
    private static string DetectImageType(byte[] data) => data.Length>=8 && data[0]==0x89 && data[1]==0x50 ? "image/png" : data.Length>=3 && data[0]==0xFF && data[1]==0xD8 ? "image/jpeg" : data.Length>=6 && data[0]==0x47 && data[1]==0x49 ? "image/gif" : "application/octet-stream";
    private string ConnectionString() => !string.IsNullOrWhiteSpace(_connectionString)
        ? _connectionString
        : throw new InvalidOperationException("ยังไม่ได้ตั้งค่า ConnectionStrings__DefaultConnection");
}
