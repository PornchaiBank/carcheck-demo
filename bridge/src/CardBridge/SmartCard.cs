using System.Runtime.InteropServices;
using System.Text;

public record CardData(string CitizenId, string ThaiName, string Address, string Reader);
public sealed class CardError(string message) : Exception(message);
public static class SmartCard
{
    [StructLayout(LayoutKind.Sequential)] struct Pci { public uint Protocol; public uint Length; }
    [DllImport("winscard.dll")] static extern int SCardEstablishContext(uint scope, IntPtr a, IntPtr b, out IntPtr context);
    [DllImport("winscard.dll")] static extern int SCardReleaseContext(IntPtr context);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] static extern int SCardListReadersW(IntPtr context, string? groups, [Out] char[]? readers, ref uint count);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] static extern int SCardConnectW(IntPtr context, string reader, uint share, uint protocols, out IntPtr card, out uint protocol);
    [DllImport("winscard.dll")] static extern int SCardDisconnect(IntPtr card, uint disposition);
    [DllImport("winscard.dll")] static extern int SCardBeginTransaction(IntPtr card);
    [DllImport("winscard.dll")] static extern int SCardEndTransaction(IntPtr card, uint disposition);
    [DllImport("winscard.dll")] static extern int SCardGetAttrib(IntPtr card, uint attribute, [Out] byte[] value, ref uint length);
    [DllImport("winscard.dll")] static extern int SCardTransmit(IntPtr card, ref Pci sendPci, byte[] send, uint sendLength, IntPtr receivePci, [Out] byte[] receive, ref uint receiveLength);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ReaderState {
        [MarshalAs(UnmanagedType.LPWStr)] public string Reader;
        public IntPtr UserData;
        public uint CurrentState;
        public uint EventState;
        public uint AtrLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst=36)] public byte[] Atr;
    }
    [DllImport("winscard.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
    static extern int SCardGetStatusChangeW(IntPtr context,uint timeout,[In,Out] ReaderState[] states,uint count);
    public static CardPresence Presence(string reader) {
        var context=Open();
        try {
            var states=new[]{ new ReaderState {Reader=reader,Atr=new byte[36]} };
            Check(SCardGetStatusChangeW(context,0,states,1));
            var state=states[0];
            if((state.EventState & (0x4u|0x8u))!=0) throw new CardError("ไม่พบเครื่องอ่าน กรุณาเชื่อมต่อ USB ใหม่");
            bool present=(state.EventState & 0x20u)!=0;
            return new(present, (state.EventState>>16).ToString()+":"+Convert.ToHexString(state.Atr.AsSpan(0,(int)Math.Min(state.AtrLength,36u))));
        } finally {SCardReleaseContext(context);}
    }
    static void Check(int code)
    {
        if (code == 0) return;
        string text = unchecked((uint)code) switch {
            0x8010000C => "ยังไม่ได้เสียบบัตร", 0x80100069 => "บัตรถูกถอดออก กรุณาเสียบใหม่",
            0x80100017 => "เครื่องอ่านยังไม่พร้อม", 0x8010001D => "Windows Smart Card service ยังไม่ทำงาน",
            0x8010000B => "เครื่องอ่านกำลังถูกโปรแกรมอื่นใช้งาน", _ => "อ่านบัตรไม่สำเร็จ" };
        throw new CardError(text + " (0x" + unchecked((uint)code).ToString("X8") + ")");
    }
    static IntPtr Open() { Check(SCardEstablishContext(0, IntPtr.Zero, IntPtr.Zero, out var context)); return context; }
    public static string[] Readers()
    {
        var ctx = Open();
        try {
            uint length = 0; var rc = SCardListReadersW(ctx, null, null, ref length);
            if (unchecked((uint)rc) == 0x8010002E) return [];
            Check(rc); if (length == 0) return [];
            var data = new char[length]; Check(SCardListReadersW(ctx, null, data, ref length));
            return new string(data).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        } finally { SCardReleaseContext(ctx); }
    }
    public static CardData Read(string reader)
    {
        var ctx = Open(); IntPtr card = IntPtr.Zero; bool transaction = false;
        try {
            Check(SCardConnectW(ctx, reader, 2, 3, out card, out var protocol));
            Check(SCardBeginTransaction(card)); transaction = true;
            var atr = new byte[64]; uint atrLength = 64;
            Check(SCardGetAttrib(card, 0x00090303, atr, ref atrLength));
            byte responseP2 = atrLength >= 2 && atr[0] == 0x3B && atr[1] == 0x67 ? (byte)1 : (byte)0;
            byte[] Send(byte[] command) {
                var pci = new Pci { Protocol = protocol, Length = 8 };
                var response = new byte[4096]; uint length = (uint)response.Length;
                Check(SCardTransmit(card, ref pci, command, (uint)command.Length, IntPtr.Zero, response, ref length));
                return response[..(int)length];
            }
            Apdu.Exchange(Send, [0x00,0xA4,0x04,0x00,0x08,0xA0,0x00,0x00,0x00,0x54,0x48,0x00,0x01], responseP2);
            byte[] Field(ushort offset, byte length) {
                var result = Apdu.Exchange(Send, [0x80,0xB0,(byte)(offset>>8),(byte)offset,0x02,0x00,length], responseP2);
                if (result.Length == 0) result = Apdu.Exchange(Send, [0x00,0xC0,0x00,responseP2,length], responseP2);
                if (result.Length != length) throw new CardError("ข้อมูลบัตรไม่ครบ กรุณาเสียบบัตรใหม่");
                return result;
            }
            var id = Encoding.ASCII.GetString(Field(0x04,13)).Trim();
            if (id.Length != 13 || id.Any(c => c < '0' || c > '9')) throw new CardError("รูปแบบเลขบัตรไม่ถูกต้อง");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var name = Encoding.GetEncoding(874).GetString(Field(0x11,100)).Trim('\0',' ');
            name = string.Join(" ", name.Split('#', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            if (string.IsNullOrWhiteSpace(name)) throw new CardError("ไม่พบชื่อบนบัตร");
            var address = Encoding.GetEncoding(874).GetString(Field(0x1579,100)).Trim('\0',' ');
            address = string.Join(" ", address.Split('#', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            return new(id,name,address,reader);
        } finally {
            if (card != IntPtr.Zero) { if(transaction) SCardEndTransaction(card,0); SCardDisconnect(card,0); }
            SCardReleaseContext(ctx);
        }
    }
}
public static class Apdu
{
    public static byte[] Exchange(Func<byte[],byte[]> send, byte[] command, byte p2)
    {
        var output = new List<byte>();
        for (var round = 0; round < 16; round++) {
            var response = send(command);
            if (response.Length < 2) throw new CardError("บัตรตอบกลับไม่ครบ");
            byte sw1 = response[^2], sw2 = response[^1];
            if (sw1 == 0x6C && command.Length == 5) { command = (byte[])command.Clone(); command[^1] = sw2; continue; }
            output.AddRange(response[..^2]);
            if (sw1 == 0x90 && sw2 == 0) return output.ToArray();
            if (sw1 == 0x61) { command = [0x00,0xC0,0x00,p2,sw2]; continue; }
            throw new CardError("บัตรไม่รองรับคำสั่งหรืออ่านไม่ได้ (SW=" + sw1.ToString("X2") + sw2.ToString("X2") + ")");
        }
        throw new CardError("บัตรตอบกลับต่อเนื่องเกินกำหนด");
    }
    public static void SelfTest()
    {
        static void Assert(bool value) { if (!value) throw new Exception("Self-test failed"); }
        Assert(Exchange(_ => [1,2,0x90,0], [0],0).SequenceEqual(new byte[]{1,2}));
        var n=0;
        var data=Exchange(cmd=> { n++; if(n==1)return [9,0x61,1]; Assert(cmd.SequenceEqual(new byte[]{0,0xC0,0,1,1})); return [8,0x90,0]; },[0],1);
        Assert(data.SequenceEqual(new byte[]{9,8}));
        n=0; Exchange(cmd=> { if(n++==0)return [0x6C,13]; Assert(cmd[^1]==13); return [0x90,0]; },[0,0xC0,0,0,1],0);
        bool failed=false; try { Exchange(_=>[0x6A,0x82],[0],0); } catch(CardError) {failed=true;} Assert(failed);
        failed=false; try { Exchange(_=>[0x61,1],[0],0); } catch(CardError) {failed=true;} Assert(failed);
        Console.WriteLine("PASS: direct response, GET RESPONSE, corrected length, status error, loop limit.");
    }
}
