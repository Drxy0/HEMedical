namespace HEMedical.Shared;

public static class CKKSParameters
{
    // TODO: Napraviti iPolyModulusDegree = 8192 (4096 vrijednosti), pa koristiti batching
    public const ulong PolyModulusDegree = 16384;
    public static readonly int[] CoeffModulusBits = [60, 60];
    public static readonly double Scale = Math.Pow(2.0, 40);
}
