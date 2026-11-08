# CARCHECK — React demo + Windows Card Bridge v3

ซอร์สล่าสุดสำหรับแก้ไขต่อใน Codex / VS Code พร้อมเว็บเดโมที่เผยแพร่แล้ว

เว็บเดโม: https://pornchaibank.github.io/carcheck-demo/

## โครงสร้าง
- `src/` — React JSX, หน้าจอ, รายงาน HTML, บันทึกการมาทำงาน และ tests
- `bridge/src/CardBridge/` — C# .NET 10 อ่านบัตรผ่าน Windows WinSCard และจำรหัสด้วย DPAPI
- `bridge/*.cmd` — Build, เปิดโปรแกรม, เปิดเชื่อมเว็บ และรีเซ็ตรหัสจับคู่
- `api/src/Carcheck.Api/` — ASP.NET Core 10 API แบบ read-only สำหรับเชื่อม SQL Server เดิมเมื่อได้รับค่าฐานข้อมูล
- `index.html`, `app.js`, `style.css`, `icons.js`, รูปและฟอนต์ — ไฟล์เว็บสำหรับ GitHub Pages; `app.js` สร้างจาก React source
- `AGENTS.md` — คำสั่งทำงานและข้อกำหนดสำหรับ Codex

## รันและแก้ไขเว็บ
ติดตั้ง Node.js 20 ขึ้นไป แล้วรันจาก root ของ repository:

```bash
npm ci
npm test
npm run dev
```

เปิด http://127.0.0.1:5173 เพื่อดู UI ระหว่างแก้ไข เมื่อพร้อมเผยแพร่:

```bash
npm run build
```

Commit source และ `app.js` ที่ build ใหม่ด้วย GitHub Pages ใช้ branch `main` และโฟลเดอร์ root อยู่แล้ว
`npm run dev` เขียน bundle สำหรับพัฒนาทับ `app.js`; ก่อน commit ต้อง build รุ่น minify อีกครั้ง

## เครื่องอ่านบัตร Windows
ติดตั้ง .NET 10 SDK x64 แล้วเปิด `bridge/Build.cmd` เมื่อสำเร็จ เปิด `bridge/Run-Web.cmd`
ทำตาม [คู่มือโปรแกรมตัวกลาง](bridge/README-TH.md) เพื่อจับคู่ในเมนู **บัญชีของฉัน → บันทึกการมาทำงาน**
รองรับการจำเครื่องอ่าน การถอด/เปลี่ยนบัตร และอ่านยืนยันก่อนกดบันทึก

Bridge อนุญาต origin ของเว็บ GitHub Pages ตามเดิม การ preview React บน localhost ยังไม่เชื่อมฮาร์ดแวร์จนกว่าจะเพิ่ม origin ที่ต้องการอย่างเจาะจงและทดสอบ

## สถานะและขอบเขต
- กู้ซอร์สจากชุด Card Bridge v3 ล่าสุดและไฟล์เว็บบน `main` เมื่อ 5 พฤศจิกายน 2569
- Login เป็นเดโมกรอกอะไรก็ได้ที่ไม่ว่าง เมนูอื่นใช้ข้อมูลสมมติ
- ข้อมูลจากบัตรและรายการมาทำงานเก็บในหน่วยความจำเท่านั้น ไม่ลงฐานข้อมูล
- จำเฉพาะรหัสเชื่อมต่อ/ชื่อเครื่องอ่านเมื่อเลือกจำการเชื่อมต่อ ไม่มีข้อมูลจริงหรือ secret อยู่ใน repository
- มีโครง API สำหรับอ่าน SQL Server เดิมแบบ parameterized query แล้ว แต่ยังไม่เชื่อมฐานจริงและยังไม่มีคำสั่งเขียน ดู `api/README-TH.md`
- การพิมพ์ใช้ HTML preview; รูปแบบรายงานเดิมยังไม่ได้ตรวจเทียบกับระบบจริง
- JavaScript tests ครอบคลุมสถานะบัตร รายการซ้ำ และการจำ/ลืมการเชื่อมต่อ ส่วน C# และเครื่องอ่านต้องทดสอบบน Windows

สำหรับ Codex ให้เปิด repository นี้ เลือก branch `main` และอ่าน `AGENTS.md` ก่อนเริ่มแก้ไข
