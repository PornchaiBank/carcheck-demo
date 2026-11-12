SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT
    database_name=DB_NAME(),
    database_user=USER_NAME(),
    default_schema=COALESCE((SELECT default_schema_name FROM sys.database_principals WHERE name=USER_NAME()),N''),
    can_create_synonym=HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE SYNONYM'),
    can_alter_dbo=HAS_PERMS_BY_NAME(N'dbo',N'SCHEMA',N'ALTER');

SELECT
    source_schema=SCHEMA_NAME(object.schema_id),
    object.name,
    object.type_desc
FROM sys.objects AS object
WHERE object.schema_id=SCHEMA_ID(N'carchecktor_car')
  AND object.name IN
  (
      N'db_declare_image',N'db_declare_text',N'db_fault',N'db_fault_des',
      N'db_province',N'db_visitcustomer',N'db_visitcustomer_head',
      N'db_visitwebsite',N'PROVINCE',N'Vw_allworking'
  )
ORDER BY object.name;

/*
  Map only legacy objects used by Carcheck.Api. Existing dbo objects are
  preserved. Synonyms keep reads and writes on the original legacy rows.
*/

IF OBJECT_ID(N'dbo.db_declare_image') IS NULL AND OBJECT_ID(N'carchecktor_car.db_declare_image',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_declare_image FOR carchecktor_car.db_declare_image;');

IF OBJECT_ID(N'dbo.db_declare_text') IS NULL AND OBJECT_ID(N'carchecktor_car.db_declare_text',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_declare_text FOR carchecktor_car.db_declare_text;');

IF OBJECT_ID(N'dbo.db_fault') IS NULL AND OBJECT_ID(N'carchecktor_car.db_fault',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_fault FOR carchecktor_car.db_fault;');

IF OBJECT_ID(N'dbo.db_fault_des') IS NULL AND OBJECT_ID(N'carchecktor_car.db_fault_des',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_fault_des FOR carchecktor_car.db_fault_des;');

IF OBJECT_ID(N'dbo.db_province') IS NULL AND OBJECT_ID(N'carchecktor_car.db_province',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_province FOR carchecktor_car.db_province;');

IF OBJECT_ID(N'dbo.db_visitcustomer') IS NULL AND OBJECT_ID(N'carchecktor_car.db_visitcustomer',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_visitcustomer FOR carchecktor_car.db_visitcustomer;');

IF OBJECT_ID(N'dbo.db_visitcustomer_head') IS NULL AND OBJECT_ID(N'carchecktor_car.db_visitcustomer_head',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_visitcustomer_head FOR carchecktor_car.db_visitcustomer_head;');

IF OBJECT_ID(N'dbo.db_visitwebsite') IS NULL AND OBJECT_ID(N'carchecktor_car.db_visitwebsite',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.db_visitwebsite FOR carchecktor_car.db_visitwebsite;');

IF OBJECT_ID(N'dbo.PROVINCE') IS NULL AND OBJECT_ID(N'carchecktor_car.PROVINCE',N'U') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.PROVINCE FOR carchecktor_car.PROVINCE;');

IF OBJECT_ID(N'dbo.Vw_allworking') IS NULL AND OBJECT_ID(N'carchecktor_car.Vw_allworking',N'V') IS NOT NULL
    EXEC(N'CREATE SYNONYM dbo.Vw_allworking FOR carchecktor_car.Vw_allworking;');

SELECT
    synonym_schema=SCHEMA_NAME(s.schema_id),
    synonym_name=s.name,
    s.base_object_name
FROM sys.synonyms AS s
WHERE s.schema_id=SCHEMA_ID(N'dbo')
  AND s.name IN
  (
      N'db_declare_image',N'db_declare_text',N'db_fault',N'db_fault_des',
      N'db_province',N'db_visitcustomer',N'db_visitcustomer_head',
      N'db_visitwebsite',N'PROVINCE',N'Vw_allworking'
  )
ORDER BY s.name;
