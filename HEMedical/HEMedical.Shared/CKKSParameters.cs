namespace HEMedical.Shared;

public static class CKKSParameters
{
    // TODO: Provjeri ove parametre
    public const ulong PolyModulusDegree = 8192;
    public static readonly int[] CoeffModulusBits = [60, 40, 40, 60];
    public static readonly double Scale = Math.Pow(2.0, 40);
}
