using System.Security.Cryptography;
using System.Text;

// No disk/database writes. All state is discarded when the bridge exits.
public record CardSnapshot(bool Present, string? ReadId, string? PersonKey, string? Name, string? MaskedId, bool Saved);
public record AttendanceRow(string Id, string PersonKey, string Day, string Name, string MaskedId, string RecordedAt, string Method);
public sealed class AttendanceSession
{
    private readonly Func<string, CardPresence> presence;
    private readonly Func<string, CardData> read;
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);
    private readonly Dictionary<string, Entry> entries = new();
    private sealed record Entry(string Stamp, string Id, string PersonKey, string Name, string MaskedId, AttendanceRow? Saved);
    public AttendanceSession(Func<string, CardPresence> presence, Func<string, CardData> read) { this.presence=presence; this.read=read; }
    public void Clear(string reader) { entries.Remove(reader); }
    private Entry? Refresh(string reader)
    {
        try {
            var before=presence(reader);
            if(!before.Present) { Clear(reader); return null; }
            // Re-read on every poll/confirmation. Never trust a stale browser name or ID.
            var card=read(reader);
            var after=presence(reader);
            if(!after.Present || before.Stamp!=after.Stamp) {Clear(reader); throw new CardError("บัตรเปลี่ยนระหว่างอ่าน กรุณารออ่านใหม่");}
            var key=Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(card.CitizenId)));
            if(entries.TryGetValue(reader,out var old) && old.Stamp==after.Stamp && old.PersonKey==key) return old;
            var masked="X-XXXX-XXXXX-"+card.CitizenId[^3..^1]+"-"+card.CitizenId[^1];
            var entry=new Entry(after.Stamp,Guid.NewGuid().ToString("N"),key,card.ThaiName,masked,null);
            entries[reader]=entry;
            return entry;
        } catch { Clear(reader); throw; }
    }
    public CardSnapshot Snapshot(string reader)
    {
        var e=Refresh(reader);
        return e==null ? new(false,null,null,null,null,false) : new(true,e.Id,e.PersonKey,e.Name,e.MaskedId,e.Saved!=null);
    }
    public AttendanceRow Confirm(string reader,string readId)
    {
        var e=Refresh(reader);
        if(e==null || e.Id!=readId) throw new CardError("บัตรถูกถอดหรือเปลี่ยนแล้ว กรุณารออ่านบัตรใหม่ก่อนบันทึก");
        if(e.Saved!=null) return e.Saved; // Idempotent retry if a response was lost.
        var now=DateTimeOffset.UtcNow;
        var row=new AttendanceRow(e.Id,e.PersonKey,now.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd"),e.Name,e.MaskedId,now.ToString("O"),"อ่านบัตรจริง · ชั่วคราว");
        entries[reader]=e with {Saved=row};
        return row;
    }
    public static void SelfTest()
    {
        bool present=true; string stamp="1"; string cid="0000000000000";
        var session=new AttendanceSession(_=>new(present,stamp),_=>new(cid,"TEST ONLY","reader"));
        void Assert(bool value){if(!value) throw new Exception("Attendance self-test failed");}
        var first=session.Snapshot("reader"); var saved=session.Confirm("reader",first.ReadId!);
        Assert(session.Confirm("reader",first.ReadId!).Id==saved.Id);
        present=false; Assert(!session.Snapshot("reader").Present);
        bool refused=false; try{session.Confirm("reader",first.ReadId!);}catch(CardError){refused=true;} Assert(refused);
        present=true;stamp="2";var next=session.Snapshot("reader");Assert(next.ReadId!=first.ReadId);
        cid="0000000000001";refused=false;try{session.Confirm("reader",next.ReadId!);}catch(CardError){refused=true;}Assert(refused);
        Console.WriteLine("PASS: idempotent confirm, removal, stale confirmation, reinsertion, changed card.");
    }
}
public record CardPresence(bool Present,string Stamp);
