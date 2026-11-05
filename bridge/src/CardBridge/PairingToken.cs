using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

// DPAPI current-user scope. Stores the pairing secret only, never card data.
public static class PairingToken
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Transform(byte[] value, bool protect)
    {
        var input=new Blob{Length=value.Length,Data=Marshal.AllocHGlobal(value.Length)};
        Blob output=default;
        try {
            Marshal.Copy(value,0,input.Data,value.Length);
            bool ok=protect?CryptProtectData(ref input,"CARCHECK pairing",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output)
                :CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte[] result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        } finally {
            if(input.Data!=IntPtr.Zero){Marshal.Copy(new byte[input.Length],0,input.Data,input.Length);Marshal.FreeHGlobal(input.Data);}
            if(output.Data!=IntPtr.Zero){Marshal.Copy(new byte[output.Length],0,output.Data,output.Length);LocalFree(output.Data);}
        }
    }
    public static string Load(bool reset)
    {
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CARCHECK","CardBridge");
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"pairing.dpapi");
        byte[] secret;
        if(!reset&&File.Exists(path)) secret=Transform(File.ReadAllBytes(path),false);
        else {
            secret=RandomNumberGenerator.GetBytes(32);
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temporary,Transform(secret,true));File.Move(temporary,path,true);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        try{if(secret.Length!=32)throw new CryptographicException("Invalid pairing token");return Convert.ToHexString(secret);}
        finally{CryptographicOperations.ZeroMemory(secret);}
    }
    public static void SelfTest()
    {
        if(!OperatingSystem.IsWindows())return;
        byte[] value=RandomNumberGenerator.GetBytes(32),roundtrip=Transform(Transform(value,true),false);
        if(!CryptographicOperations.FixedTimeEquals(value,roundtrip))throw new Exception("DPAPI roundtrip failed");
        CryptographicOperations.ZeroMemory(value);CryptographicOperations.ZeroMemory(roundtrip);
        Console.WriteLine("PASS: Windows current-user pairing encryption.");
    }
}
