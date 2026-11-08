SET NOCOUNT ON;
SET XACT_ABORT ON;

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

PRINT N'สร้างตารางเชื่อม LINE และประวัติแจ้งเตือนเรียบร้อยแล้ว';
