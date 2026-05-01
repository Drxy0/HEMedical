using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using Microsoft.Research.SEAL;

namespace HEMedical.Hospital.Services;

public class EncryptionService : IEncryptionService
{
    private readonly IHEPublicKeyService _keyService;

    public EncryptionService(IHEPublicKeyService keyService)
    {
        _keyService = keyService;
    }

    public EncryptedAverageResult Encrypt(List<decimal> values)
    {
        SEALContext context = _keyService.GetContext();
        using var encryptor = new Encryptor(context, _keyService.PublicKey);
        using var encoder = new CKKSEncoder(context);

        ulong slotCount = encoder.SlotCount;
        List<double> valueVector = new((int)slotCount);
        List<double> onesVector = new((int)slotCount);

        for (int i = 0; i < (int)slotCount; i++)
        {
            if (i < values.Count)
            {
                valueVector.Add((double)values[i]);
                onesVector.Add(1.0);
            }
            else
            {
                valueVector.Add(0.0);
                onesVector.Add(0.0);
            }
        }

        // Encode and encrypt values
        using var valuePlain = new Plaintext();
        encoder.Encode(valueVector, CKKSParameters.Scale, valuePlain);
        using var valueCipher = new Ciphertext();
        encryptor.Encrypt(valuePlain, valueCipher);

        // Encode and encrypt ones
        using var onesPlain = new Plaintext();
        encoder.Encode(onesVector, CKKSParameters.Scale, onesPlain);
        using var onesCipher = new Ciphertext();
        encryptor.Encrypt(onesPlain, onesCipher);

        // Serialize both to byte arrays
        using var valueStream = new MemoryStream();
        valueCipher.Save(valueStream);

        using var onesStream = new MemoryStream();
        onesCipher.Save(onesStream);

        return new EncryptedAverageResult(valueStream.ToArray(), onesStream.ToArray());
    }
}
