using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace RoyalIdentity.Utils;

public class ECKeyHelper
{
    /// <summary>
    /// Exports the ECParameters to an XML string.
    /// </summary>
    /// <param name="ecdsa">The ECDsa instance from which to export the parameters.</param>
    /// <param name="includePrivateParameters">Whether to include private parameters.</param>
    /// <returns>The XML string representing the ECParameters.</returns>
    public static string ExportECParametersToXml(ECDsa ecdsa, bool includePrivateParameters)
    {
        ECParameters ecParameters = ecdsa.ExportParameters(includePrivateParameters);

        XElement xml = new XElement("ECParameters",
            new XElement("Curve",
                new XElement("CurveType", ecParameters.Curve.Oid.Value)
            ),
            new XElement("Q",
                new XElement("X", Convert.ToBase64String(ecParameters.Q.X!)),
                new XElement("Y", Convert.ToBase64String(ecParameters.Q.Y!))
            )
        );

        // Add the private key (D) if requested and available
        if (includePrivateParameters && ecParameters.D != null)
        {
            xml.Add(new XElement("D", Convert.ToBase64String(ecParameters.D)));
        }

        return xml.ToString();
    }

    /// <summary>
    /// Imports the ECParameters from an XML string.
    /// </summary>
    /// <param name="xmlString">The XML string representing the ECParameters.</param>
    /// <returns>The imported ECParameters.</returns>
    public static ECParameters ImportECParametersFromXml(string xmlString)
    {
        XElement xml = XElement.Parse(xmlString);

        ECParameters ecParameters = new ECParameters
        {
            Curve = ECCurve.CreateFromOid(new Oid(xml.Element("Curve").Element("CurveType").Value)),
            Q = new ECPoint
            {
                X = Convert.FromBase64String(xml.Element("Q").Element("X").Value),
                Y = Convert.FromBase64String(xml.Element("Q").Element("Y").Value)
            }
        };

        // Check if the private key (D) exists in the XML and set it if present
        var dElement = xml.Element("D");
        if (dElement != null)
        {
            ecParameters.D = Convert.FromBase64String(dElement.Value);
        }

        return ecParameters;
    }

    /// <summary>
    /// Creates an ECDsaSecurityKey from the ECParameters XML.
    /// </summary>
    /// <param name="xmlString">The XML string representing the ECParameters.</param>
    /// <returns>An ECDsaSecurityKey based on the imported parameters.</returns>
    public static ECDsaSecurityKey CreateECDsaSecurityKeyFromXml(string xmlString)
    {
        ECParameters ecParameters = ImportECParametersFromXml(xmlString);
        ECDsa ecdsa = ECDsa.Create(ecParameters);
        return new ECDsaSecurityKey(ecdsa);
    }

    public static string SerializeECParameters(ECParameters ecParameters)
    {
        // Serializa os ECParameters de forma personalizada
        return JsonSerializer.Serialize(new ECParametersSerializable(ecParameters));
    }

    public static ECParameters DeserializeECParameters(string json)
    {
        // Desserializa e retorna os ECParameters
        return JsonSerializer.Deserialize<ECParametersSerializable>(json)!.ToECParameters();
    }

    private class ECParametersSerializable
    {
        public ECParametersSerializable() { }

        public ECParametersSerializable(ECParameters ecParameters)
        {
            Q = new ECPointSerializable(ecParameters.Q);
            D = ecParameters.D != null ? Convert.ToBase64String(ecParameters.D) : null;

            if (ecParameters.Curve.IsNamed)
            {
                CurveOid = ecParameters.Curve.Oid.Value!;
            }
            else
            {
                throw new PlatformNotSupportedException("Only named curves are supported in this implementation.");
            }
        }

        public ECPointSerializable Q { get; set; }
        public string? D { get; set; }
        public string CurveOid { get; set; }

        public ECParameters ToECParameters()
        {
            var ecParameters = new ECParameters
            {
                Q = Q.ToECPoint(),
                D = D != null ? Convert.FromBase64String(D) : null,
            };

            if (!string.IsNullOrEmpty(CurveOid))
            {
                ecParameters.Curve = ECCurve.CreateFromValue(CurveOid);
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported curve type.");
            }

            return ecParameters;
        }
    }

    private class ECPointSerializable
    {
        public ECPointSerializable() { }

        public ECPointSerializable(ECPoint point)
        {
            X = Convert.ToBase64String(point.X!);
            Y = Convert.ToBase64String(point.Y!);
        }

        public string X { get; set; }
        public string Y { get; set; }

        public ECPoint ToECPoint()
        {
            return new ECPoint
            {
                X = Convert.FromBase64String(X),
                Y = Convert.FromBase64String(Y)
            };
        }
    }
}

