namespace HEMedical.Shared;

public static class CKKSParameters
{
    // TODO: Napraviti iPolyModulusDegree = 8192 (sloutCount je 4096 vrijednosti), pa koristiti batching
    public const ulong PolyModulusDegree = 4096;
    public static readonly int[] CoeffModulusBits = [60, 49];
    public static readonly double Scale = Math.Pow(2.0, 40);
}
