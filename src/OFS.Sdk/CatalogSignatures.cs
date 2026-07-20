using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OFS.Sdk;

public sealed record SignedModCatalog
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("keyId")]
    public required string KeyId { get; init; }

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = ModCatalogSignatures.Algorithm;

    [JsonPropertyName("payload")]
    public required string Payload { get; init; }

    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

public sealed record ModCatalogTrustStore
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("keys")]
    public IReadOnlyList<ModCatalogTrustKey> Keys { get; init; } = [];
}

public sealed record ModCatalogTrustKey
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; init; } = ModCatalogSignatures.Algorithm;

    [JsonPropertyName("subjectPublicKeyInfo")]
    public required string SubjectPublicKeyInfo { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}

public sealed record ModCatalogSignatureResult(
    bool Success,
    ModCatalog? Catalog,
    string? KeyId,
    string? KeyFingerprint,
    IReadOnlyList<string> Errors);

/// <summary>
/// Detached-semantics catalog signing carried in one JSON envelope. The exact
/// UTF-8 catalog bytes are base64 encoded and signed, avoiding ambiguous JSON
/// canonicalization rules.
/// </summary>
public static class ModCatalogSignatures
{
    public const int CurrentSchemaVersion = 1;
    public const string Algorithm = "ECDSA_P256_SHA256";
    public const int MaximumPayloadBytes = 8 * 1024 * 1024;
    private const int P1363SignatureBytes = 64;

    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static SignedModCatalog Sign(
        ReadOnlySpan<byte> catalogJson,
        string keyId,
        ECDsa privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ValidateKeyId(keyId);
        if (catalogJson.IsEmpty || catalogJson.Length > MaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(catalogJson),
                $"Catalog payload must contain 1-{MaximumPayloadBytes} bytes.");
        }
        EnsureP256(privateKey);

        var catalog = JsonSerializer.Deserialize<ModCatalog>(catalogJson, CatalogJsonOptions);
        var errors = ModCatalogValidator.Validate(catalog);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(
                $"Catalog cannot be signed because it is invalid: {string.Join(" ", errors)}");
        }

        var signature = privateKey.SignData(
            catalogJson,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (signature.Length != P1363SignatureBytes)
        {
            throw new CryptographicException("ECDSA P-256 produced an unexpected signature length.");
        }
        return new SignedModCatalog
        {
            KeyId = keyId,
            Payload = Convert.ToBase64String(catalogJson),
            Signature = Convert.ToBase64String(signature),
        };
    }

    public static ModCatalogSignatureResult Verify(
        SignedModCatalog? envelope,
        ModCatalogTrustStore? trustStore)
    {
        var errors = ValidateEnvelope(envelope);
        if (envelope is null)
        {
            return new ModCatalogSignatureResult(false, null, null, null, errors);
        }

        var trustErrors = ValidateTrustStore(trustStore);
        if (trustErrors.Count != 0)
        {
            errors.AddRange(trustErrors);
            return new ModCatalogSignatureResult(false, null, envelope.KeyId, null, errors);
        }
        if (errors.Count != 0)
        {
            return new ModCatalogSignatureResult(false, null, envelope.KeyId, null, errors);
        }

        var trusted = trustStore!.Keys.SingleOrDefault(key =>
            string.Equals(key.Id, envelope.KeyId, StringComparison.Ordinal));
        if (trusted is null)
        {
            return new ModCatalogSignatureResult(
                false,
                null,
                envelope.KeyId,
                null,
                [$"Catalog signing key '{envelope.KeyId}' is not trusted."]);
        }

        try
        {
            var payload = Convert.FromBase64String(envelope.Payload);
            var signature = Convert.FromBase64String(envelope.Signature);
            var subjectPublicKeyInfo = Convert.FromBase64String(trusted.SubjectPublicKeyInfo);
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out var bytesRead);
            if (bytesRead != subjectPublicKeyInfo.Length)
            {
                return Failure(envelope.KeyId, trusted.Sha256, "Trusted public key contains trailing data.");
            }
            EnsureP256(key);
            if (!key.VerifyData(
                    payload,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            {
                return Failure(envelope.KeyId, trusted.Sha256, "Catalog signature is invalid.");
            }

            var catalog = JsonSerializer.Deserialize<ModCatalog>(payload, CatalogJsonOptions);
            var catalogErrors = ModCatalogValidator.Validate(catalog);
            return catalog is not null && catalogErrors.Count == 0
                ? new ModCatalogSignatureResult(
                    true,
                    catalog,
                    envelope.KeyId,
                    trusted.Sha256,
                    [])
                : new ModCatalogSignatureResult(
                    false,
                    null,
                    envelope.KeyId,
                    trusted.Sha256,
                    catalogErrors);
        }
        catch (Exception exception) when (exception is
            FormatException or CryptographicException or JsonException)
        {
            return Failure(envelope.KeyId, trusted.Sha256, exception.Message);
        }
    }

    public static ModCatalogTrustKey ExportTrustKey(string keyId, ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ValidateKeyId(keyId);
        EnsureP256(publicKey);
        var bytes = publicKey.ExportSubjectPublicKeyInfo();
        return new ModCatalogTrustKey
        {
            Id = keyId,
            SubjectPublicKeyInfo = Convert.ToBase64String(bytes),
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        };
    }

    public static IReadOnlyList<string> ValidateTrustStore(ModCatalogTrustStore? trustStore)
    {
        var errors = new List<string>();
        if (trustStore is null)
        {
            errors.Add("Catalog trust store deserialized to null.");
            return errors;
        }
        if (trustStore.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported trust store schemaVersion {trustStore.SchemaVersion}; expected 1.");
        }
        if (trustStore.Keys is null)
        {
            errors.Add("Catalog trust store keys must be an array.");
            return errors;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in trustStore.Keys)
        {
            if (!ModManifestValidator.IsValidId(key.Id) || !ids.Add(key.Id))
            {
                errors.Add($"Trust store key id '{key.Id}' is invalid or duplicated.");
            }
            if (!string.Equals(key.Algorithm, Algorithm, StringComparison.Ordinal))
            {
                errors.Add($"Trust store key '{key.Id}' uses unsupported algorithm '{key.Algorithm}'.");
            }
            try
            {
                var bytes = Convert.FromBase64String(key.SubjectPublicKeyInfo);
                var fingerprint = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(fingerprint, key.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Trust store key '{key.Id}' fingerprint does not match its public key.");
                    continue;
                }
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(bytes, out var read);
                if (read != bytes.Length || ecdsa.KeySize != 256)
                {
                    errors.Add($"Trust store key '{key.Id}' is not an exact ECDSA P-256 public key.");
                }
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                errors.Add($"Trust store key '{key.Id}' is invalid: {exception.Message}");
            }
        }
        return errors;
    }

    private static List<string> ValidateEnvelope(SignedModCatalog? envelope)
    {
        var errors = new List<string>();
        if (envelope is null)
        {
            errors.Add("Signed catalog deserialized to null.");
            return errors;
        }
        if (envelope.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported signed catalog schemaVersion {envelope.SchemaVersion}; expected 1.");
        }
        if (!ModManifestValidator.IsValidId(envelope.KeyId))
        {
            errors.Add("Signed catalog keyId is invalid.");
        }
        if (!string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal))
        {
            errors.Add($"Unsupported catalog signature algorithm '{envelope.Algorithm}'.");
        }
        try
        {
            var payload = Convert.FromBase64String(envelope.Payload);
            if (payload.Length is 0 or > MaximumPayloadBytes)
            {
                errors.Add("Signed catalog payload size is invalid.");
            }
        }
        catch (FormatException)
        {
            errors.Add("Signed catalog payload is not valid base64.");
        }
        try
        {
            if (Convert.FromBase64String(envelope.Signature).Length != P1363SignatureBytes)
            {
                errors.Add("Signed catalog signature must be a 64-byte P1363 value.");
            }
        }
        catch (FormatException)
        {
            errors.Add("Signed catalog signature is not valid base64.");
        }
        return errors;
    }

    private static void ValidateKeyId(string keyId)
    {
        if (!ModManifestValidator.IsValidId(keyId))
        {
            throw new ArgumentException("Catalog signing key id is invalid.", nameof(keyId));
        }
    }

    private static void EnsureP256(ECDsa key)
    {
        if (key.KeySize != 256)
        {
            throw new CryptographicException("Catalog signing requires an ECDSA P-256 key.");
        }
    }

    private static ModCatalogSignatureResult Failure(
        string keyId,
        string? fingerprint,
        string error) => new(false, null, keyId, fingerprint, [error]);
}
