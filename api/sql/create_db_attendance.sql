/*
  CARCHECK - ตารางบันทึกการมาทำงาน
  สำหรับ Microsoft SQL Server

  วิธีใช้:
  1. เลือกฐานข้อมูล CARCHECK เดิมใน SSMS
  2. เปิดไฟล์นี้ แล้วกด Execute
  3. สคริปต์สามารถรันซ้ำได้ โดยจะไม่ลบข้อมูลเดิม
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.db_attendance', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.db_attendance
        (
            id                  bigint IDENTITY(1,1) NOT NULL,
            bridge_read_id      nvarchar(100) NOT NULL,
            person_key          nvarchar(200) NOT NULL,
            employee_name       nvarchar(250) NOT NULL,
            masked_citizen_id   nvarchar(30) NOT NULL,
            recorded_at         datetimeoffset(0) NOT NULL,
            work_day            date NOT NULL,
            method              nvarchar(50) NOT NULL,
            center_id           varchar(200) NOT NULL,
            recorded_by         varchar(200) NOT NULL,
            created_at          datetime2(0) NOT NULL
                CONSTRAINT DF_db_attendance_created_at DEFAULT SYSDATETIME(),

            CONSTRAINT PK_db_attendance
                PRIMARY KEY CLUSTERED (id),

            CONSTRAINT UQ_db_attendance_read
                UNIQUE (bridge_read_id),

            CONSTRAINT UQ_db_attendance_person_day
                UNIQUE (person_key, work_day)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.db_attendance')
          AND name = N'IX_db_attendance_center_day'
    )
    BEGIN
        CREATE INDEX IX_db_attendance_center_day
            ON dbo.db_attendance
            (
                center_id,
                work_day DESC,
                recorded_at DESC
            );
    END;

    COMMIT TRANSACTION;
    PRINT N'สร้าง/ตรวจสอบตาราง dbo.db_attendance เรียบร้อยแล้ว';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT
    id,
    bridge_read_id,
    person_key,
    employee_name,
    masked_citizen_id,
    recorded_at,
    work_day,
    method,
    center_id,
    recorded_by,
    created_at
FROM dbo.db_attendance
ORDER BY recorded_at DESC, id DESC;
