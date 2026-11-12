namespace Carcheck.Api;

public sealed class VehicleCopyDuplicateException : Exception
{
    public const string UserMessage="ทะเบียนรถนี้มีคู่มือ/สำเนารถอยู่แล้วในศูนย์บริการนี้ ไม่สามารถบันทึกซ้ำได้";
    public VehicleCopyDuplicateException():base(UserMessage){}
}
