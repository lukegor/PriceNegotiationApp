using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace PriceNegotiationApp.Modules.Identity.Features.Auth;

/// <summary>
/// Holds the ES256 private key PEM and derives the public JWK once.
/// Only PublicJwk ever leaves this class: bearer validation and the JWKS
/// endpoint can verify tokens without holding signing material.
/// </summary>
internal sealed class EcSigningKey
{
    // Literal JOSE name: SecurityAlgorithms.EcdsaSha256* hold the XML-DSig URI,
    // which JwtSecurityTokenHandler would copy verbatim into the token header.
    public const string Algorithm = "ES256";

    private const string Usage =
        "Jwt:PrivateKey must be an EC P-256 private key in PKCS#8 PEM "
        + "(generate: openssl ecparam -name prime256v1 -genkey -noout).";

    private readonly string _privateKeyPem;

    internal JsonWebKey PublicJwk { get; }

    internal string Kid { get; }

    public EcSigningKey(IOptions<JwtOptions> options)
    {
        _privateKeyPem = Normalize(options.Value.PrivateKey);
        using var ecdsa = Import(_privateKeyPem);

        // Pure-data JWK built from exported coordinates: nothing here references
        // the disposed probe instance, and verifiers construct their own providers.
        var publicKey = ecdsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonWebKey
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Crv = "P-256",
            X = Base64UrlEncoder.Encode(publicKey.Q.X),
            Y = Base64UrlEncoder.Encode(publicKey.Q.Y),
        };
        Kid = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
        jwk.Kid = Kid;
        PublicJwk = jwk;
    }

    internal ECDsa CreatePrivateEcdsa() => Import(_privateKeyPem);

    private static ECDsa Import(string pem)
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (CryptographicException ex)
        {
            ecdsa.Dispose();
            throw new InvalidOperationException(Usage, ex);
        }

        if (ecdsa.KeySize != 256)
        {
            ecdsa.Dispose();
            throw new InvalidOperationException(Usage);
        }

        return ecdsa;
    }

    private static string Normalize(string raw) => raw.Replace("\\n", "\n").Trim();
}
