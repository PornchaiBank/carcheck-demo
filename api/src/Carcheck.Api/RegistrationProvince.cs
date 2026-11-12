namespace Carcheck.Api;

public static class RegistrationProvince
{
    public static string Name(string? value,IReadOnlyDictionary<int,string> provinces)
    {
        var text=value?.Trim()??string.Empty;
        return int.TryParse(text,out var code)&&provinces.TryGetValue(code,out var name)?name:text;
    }
}
