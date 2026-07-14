namespace HEMedical.HospitalProxy.Services;

/// <summary>
/// The bin-slot mapping shared by the encrypted and the plaintext histogram builders:
/// slot b counts bin b ([binStart + b·binWidth, binStart + (b+1)·binWidth)), slot
/// binCount the underflow, slot binCount+1 the overflow. One shared function guarantees
/// a value lands in the same slot on both sides of the verification split.
/// </summary>
internal static class HistogramBinning
{
    public static int SlotFor(double value, double binStart, double binWidth, int binCount)
    {
        if (value < binStart)
            return binCount;        // underflow

        int bin = (int)((value - binStart) / binWidth);
        return bin >= binCount
            ? binCount + 1          // overflow
            : bin;
    }
}
