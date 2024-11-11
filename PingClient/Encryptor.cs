using System.Security.Cryptography;
using System.Text;

namespace PingClient
{
    public class Encryptor
    {
        private ECDiffieHellman? _diffieHellman;
        private byte[]? _publicKey;
        private byte[]? _sharedKey;

        public Encryptor()
        {
            GenerateNewKeyPair();
        }

        public byte[]? PublicKey => _publicKey;

        public void GenerateSharedKey(byte[] otherPublicKey)
        {
            using var otherKey = ECDiffieHellman.Create();
            otherKey.ImportSubjectPublicKeyInfo(otherPublicKey, out _);
            _sharedKey = _diffieHellman!.DeriveKeyMaterial(otherKey.PublicKey);
        }

        public byte[] Encrypt(string plainText)
        {
            if (_sharedKey == null)
            {
                throw new InvalidOperationException("Shared key has not been established.");
            }

            using var aes = Aes.Create();
            aes.Key = _sharedKey;
            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, iv.Length, cipherBytes.Length);

            return result;
        }

        public string Decrypt(byte[] cipherText)
        {
            if (_sharedKey == null)
            {
                throw new InvalidOperationException("Shared key has not been established.");
            }

            using var aes = Aes.Create();
            aes.Key = _sharedKey;

            var iv = new byte[aes.BlockSize / 8];
            var cipherBytes = new byte[cipherText.Length - iv.Length];

            Buffer.BlockCopy(cipherText, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(cipherText, iv.Length, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }

        public void GenerateNewKeyPair()
        {
            _diffieHellman = ECDiffieHellman.Create();
            _publicKey = _diffieHellman.ExportSubjectPublicKeyInfo();
        }
    }
}