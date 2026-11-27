using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

/// <summary>
/// A stand-in for DPAPI in tests. It XORs every byte with <c>0x5A</c>, and when
/// <paramref name="failUnprotect"/> is set, <see cref="Unprotect"/> throws as DPAPI does after a
/// profile reset.
/// </summary>
internal sealed class FakeKeyProtector(string id = "fake", bool failUnprotect = false) : IKeyProtector
{
    public string Id { get; } = id;

    public byte[] Protect(ReadOnlySpan<byte> secret) => Xor(secret);

    public byte[] Unprotect(ReadOnlySpan<byte> protectedData) =>
        failUnprotect ? throw new CryptographicException("The fake protector was told to fail.") : Xor(protectedData);

    private static byte[] Xor(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ 0x5A);
        }

        return result;
    }
}
