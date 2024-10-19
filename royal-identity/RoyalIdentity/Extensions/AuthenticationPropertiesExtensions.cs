using System.Text;
using Microsoft.AspNetCore.Authentication;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extensions for AuthenticationProperties
/// </summary>
public static class AuthenticationPropertiesExtensions
{
    internal const string SessionIdKey = "session_id";
    internal const string ClientListKey = "client_list";

    /// <summary>
    /// Gets the user's session identifier.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static string? GetSessionId(this AuthenticationProperties properties)
    {
        if (properties?.Items.ContainsKey(SessionIdKey) == true)
        {
            return properties.Items[SessionIdKey];
        }

        return null;
    }

    /// <summary>
    /// Sets the user's session identifier.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="sid">The session id</param>
    /// <returns></returns>
    public static void SetSessionId(this AuthenticationProperties properties, string sid)
    {
        properties.Items[SessionIdKey] = sid;
    }

    /// <summary>
    /// Gets the list of client ids the user has signed into during their session.
    /// </summary>
    /// <param name="properties"></param>
    /// <returns></returns>
    public static List<string> GetClientList(this AuthenticationProperties? properties)
    {
        if (properties is null
            || !properties.Items.TryGetValue(ClientListKey, out var value)
            || value is null)
        {
            return [];
        }

        return DecodeList(value);

    }

    /// <summary>
    /// Removes the list of client ids.
    /// </summary>
    /// <param name="properties"></param>
    public static void RemoveClientList(this AuthenticationProperties properties)
    {
        properties?.Items.Remove(ClientListKey);
    }

    /// <summary>
    /// Adds a client to the list of clients the user has signed into during their session.
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="clientId"></param>
    public static void AddClientId(this AuthenticationProperties properties, string clientId)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        var clients = properties.GetClientList();
        if (clients.Contains(clientId))
            return;

        clients.Add(clientId);

        var value = EncodeList(clients);
        if (value is null)
        {
            properties.Items.Remove(ClientListKey);
        }
        else
        {
            properties.Items[ClientListKey] = value;
        }
    }


    private static List<string> DecodeList(string value)
    {
        if (!value.IsPresent())
            return [];

        var bytes = Base64Url.Decode(value);
        value = Encoding.UTF8.GetString(bytes);
        return Json.Deserialize<List<string>>(value);
    }

    private static string? EncodeList(IEnumerable<string>? list)
    {
        if (list is null || !list.Any())
            return null;

        var value = Json.Serialize(list);
        var bytes = Encoding.UTF8.GetBytes(value);
        value = Base64Url.Encode(bytes);
        return value;

    }

    
}