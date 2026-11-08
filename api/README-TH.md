# CARCHECK API — ตัวเชื่อมฐานข้อมูลเดิม

## การใช้งานผ่านอินเทอร์เน็ต

Web อ่าน URL ของ API จากไฟล์ `config.js` ที่ root ของเว็บไซต์ ให้เปลี่ยนเป็น Public HTTPS URL ก่อน Deploy GitHub Pages:

```javascript
window.CARCHECK_CONFIG={
  apiRoot:"https://api.example.com"
};
```

ตั้งค่าฝั่ง API ผ่าน Environment Variables เท่านั้น ห้ามใส่ Password หรือ Token จริงใน `appsettings.json`:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Production'
$env:ASPNETCORE_URLS='http://127.0.0.1:5188'
$env:ConnectionStrings__DefaultConnection='Server=tcp:SERVER,1433;Database=CarCheckTor;User Id=api_user;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connect Timeout=15;'
$env:Api__Token='RANDOM_TOKEN_FOR_SERVICE_ACCESS'
$env:Api__AllowedOrigins__0='https://pornchaibank.github.io'
dotnet run --project api/src/Carcheck.Api --no-launch-profile
```

Public HTTPS proxy หรือ Cloudflare Tunnel ต้องส่ง `X-Forwarded-Proto: https` เพื่อให้ API ออก Session Cookie แบบ `Secure` และ `SameSite=None` สำหรับ GitHub Pages

API นี้เป็น ASP.NET Core 10 สำหรับอ่านข้อมูลจาก SQL Server เดิมผ่าน HTTPS โดย React ไม่เชื่อมฐานข้อมูลโดยตรง

## สถานะ

- เปิดเฉพาะ endpoint แบบอ่านข้อมูล
- ใช้ parameterized SQL
- ซ่อนเลขบัตรประชาชนก่อนส่งออก
- บังคับ Bearer token และ exact-origin CORS
- มี Login สำหรับ Local compatibility โดยตรวจบัญชีเดิมใน `db_staff` และเปรียบเทียบ MD5 ภายใน API เท่านั้น
- ยังไม่มี endpoint เขียนข้อมูล, SMS หรือ LINE
- ชื่อตาราง/คอลัมน์อ้างอิงจาก Web Forms เดิม ต้องตรวจเทียบ schema จริงก่อนใช้งาน

ตรวจเทียบกับไฟล์ schema ที่ได้รับแล้ว ดูรายการและข้อควรระวังใน `LEGACY-SCHEMA.md`

## ตั้งค่าในเครื่องพัฒนา

อย่าใส่รหัสผ่านใน repository ให้ตั้งผ่าน environment variables หรือ Secret Manager:

```powershell
$env:ConnectionStrings__DefaultConnection='Server=tcp:100.80.69.97,1433;Database=CarCheckTor;User Id=api_user;Password=YOUR_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connect Timeout=15;'
$env:Api__Token='สร้างโทเคนแบบสุ่มอย่างน้อย 32 ไบต์'
$env:Api__AllowedOrigins__1='http://127.0.0.1:5173'
$env:Api__AllowLocalDevelopmentWithoutToken='true'
dotnet run --project api/src/Carcheck.Api
```

ตัวเลือกไม่ใช้ token ทำงานเฉพาะเมื่อ request มาจาก loopback และมี origin ตรงกับ `http://127.0.0.1:5173` เท่านั้น ห้ามเปิดในการ deploy จริง

สำหรับ SQL Server ในเครื่องเดียวกันด้วย Windows Authentication:

```powershell
$env:ConnectionStrings__DefaultConnection='Server=.;Database=CarCheckTor;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

`TrustServerCertificate=True` ใช้สำหรับ local development certificate เท่านั้น production ต้องใช้ certificate ที่ตรวจสอบได้และตั้งเป็น `False`

ทดสอบ health check โดยไม่ใช้ข้อมูลฐาน:

```powershell
Invoke-RestMethod http://127.0.0.1:5000/api/health
```

เมื่อกำหนด token และ connection string แล้ว ใช้ `GET /api/database/health` เพื่อตรวจการเชื่อมต่อและยืนยันว่าพบ `db_working` กับ `Vw_allworking` โดยไม่มีการเขียนข้อมูล

ก่อนต่อฐานจริง ให้ใช้บัญชี read-only, เปิดเฉพาะ IP ที่ต้องใช้ และสำรองฐานข้อมูลก่อนเสมอ
