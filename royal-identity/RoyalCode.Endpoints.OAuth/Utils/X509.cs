using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace RoyalIdentity.Utils;

public static class X509
{
    public static X509CertificatesLocation CurrentUser => new X509CertificatesLocation(StoreLocation.CurrentUser);
    public static X509CertificatesLocation LocalMachine => new X509CertificatesLocation(StoreLocation.LocalMachine);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public class X509CertificatesLocation
{
    private readonly StoreLocation _location;

    public X509CertificatesLocation(StoreLocation location)
    {
        _location = location;
    }

    public X509CertificatesName My => new X509CertificatesName(_location, StoreName.My);
    public X509CertificatesName AddressBook => new X509CertificatesName(_location, StoreName.AddressBook);
    public X509CertificatesName TrustedPeople => new X509CertificatesName(_location, StoreName.TrustedPeople);
    public X509CertificatesName TrustedPublisher => new X509CertificatesName(_location, StoreName.TrustedPublisher);
    public X509CertificatesName CertificateAuthority => new X509CertificatesName(_location, StoreName.CertificateAuthority);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public class X509CertificatesName
{
    private readonly StoreLocation _location;
    private readonly StoreName _name;

    public X509CertificatesName(StoreLocation location, StoreName name)
    {
        _location = location;
        _name = name;
    }

    public X509CertificatesFinder Thumbprint => new X509CertificatesFinder(_location, _name, X509FindType.FindByThumbprint);
    public X509CertificatesFinder SubjectDistinguishedName => new X509CertificatesFinder(_location, _name, X509FindType.FindBySubjectDistinguishedName);
    public X509CertificatesFinder SerialNumber => new X509CertificatesFinder(_location, _name, X509FindType.FindBySerialNumber);
    public X509CertificatesFinder IssuerName => new X509CertificatesFinder(_location, _name, X509FindType.FindByIssuerName);
}

[EditorBrowsable(EditorBrowsableState.Never)]
public class X509CertificatesFinder
{
    private readonly StoreLocation _location;
    private readonly StoreName _name;
    private readonly X509FindType _findType;

    public X509CertificatesFinder(StoreLocation location, StoreName name, X509FindType findType)
    {
        _location = location;
        _name = name;
        _findType = findType;
    }

    public IEnumerable<X509Certificate2> Find(object findValue, bool validOnly = true)
    {
        using var store = new X509Store(_name, _location);
        store.Open(OpenFlags.ReadOnly);

        var certColl = store.Certificates.Find(_findType, findValue, validOnly);
        return certColl.Cast<X509Certificate2>();
    }
}