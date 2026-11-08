# Legacy SQL Server schema inventory

ตรวจจาก `db_carchecktor.sql` วันที่ 6 พฤศจิกายน 2569 ฐานข้อมูลเดิมชื่อ `carchecktor_carcheck` ไฟล์ที่ได้รับเป็น DDL เท่านั้น ไม่มีข้อมูลจริงหรือ credentials สำหรับเชื่อมต่อ

## วัตถุในฐานข้อมูล

ตารางหลักของ workflow:

- `db_center`, `db_customer`, `db_insurer`, `db_typeact`, `db_working`
- `db_fault_des`, `db_fault`
- `db_staff`, `db_program`, `db_programlist`, `db_security`
- `db_province`, `PROVINCE`, `Data`
- `db_announce`, `db_declare_image`, `db_declare_text`
- `db_visitcustomer`, `db_visitcustomer_head`, `db_visitwebsite`
- `db_outsider`, `db_sendsms`, `db_syslog`

ตารางสรุป/ชั่วคราว: `db_countact`, `db_countprovince`

ตารางที่ดูเป็นตัวอย่างหรือโค้ดทดลอง: `Contact_test`, `Contacts`, `Menus`, `user_test`

Views: `Vw_allworking`, `Vw_allcar`, `Vw_fault`, `Vw_fault_all`, `Vw_fault_img`

Stored procedures: `sp_countprovince`, `sp_data_act`

## Mapping ที่ API ใช้

- รายการรับงาน: `Vw_allworking`
- ลูกค้า: `db_customer` ส่งข้อมูลให้หน้าค้นหาภายในหลังเข้าสู่ระบบ รวมเลขบัตรและที่อยู่ตามที่ผู้ใช้ร้องขอ
- ศูนย์/บริษัทประกัน/ประเภท รย./จังหวัด: ตาราง master ที่ชื่อตรงกัน
- ข้อบกพร่อง: `db_fault_des` join `db_fault` โดยส่งเพียงสถานะว่ามีรูป ไม่ส่ง binary
- เมนูรายผู้ใช้: `db_programlist` join `db_program`

## ประเด็นที่ต้องแก้ก่อน production

- ไม่มี foreign keys ใน DDL จึงต้องตรวจ orphan records และเพิ่ม constraint หลังสำรอง/ทำความสะอาดข้อมูล
- `sp_countprovince` และ `sp_data_act` ลบข้อมูลทั้งตารางสรุปร่วมก่อนคำนวณ ทำให้ผู้ใช้หลายศูนย์ชนกันได้ API จะไม่เรียก procedures เหล่านี้
- `db_staff.User_password` เป็น MD5 เดิม ใช้ได้เฉพาะ Local compatibility login ชั่วคราว โดยเปรียบเทียบภายใน API และห้ามส่ง hash ผ่าน API; production ต้องย้ายไป password hash ที่แข็งแรงกว่า
- `db_sendsms` มีช่องเก็บรหัสบริการ SMS ห้าม expose ผ่าน API
- หลายคอลัมน์ใช้ `varchar` กับข้อความไทยและวันที่บางส่วนเก็บเป็นข้อความ ควรวาง migration ไป `nvarchar`/ชนิดวันที่ที่เหมาะสมภายหลัง
- `image` เป็นชนิดข้อมูลเลิกใช้แล้ว ควรวาง migration เป็น `varbinary(max)`
