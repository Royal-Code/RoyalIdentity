using Microsoft.Extensions.Logging;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using System.Text.Json;
using RoyalIdentity.Contexts;
using Microsoft.AspNetCore.CookiePolicy;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Models;

namespace RoyalIdentity.Extensions;

public static class LoggerExtensions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    static LoggerExtensions()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Serializes the specified object.
    /// </summary>
    /// <param name="logObject">The object.</param>
    /// <returns></returns>
    private static string Serialize(object logObject)
    {
        return JsonSerializer.Serialize(logObject, Options);
    }

    internal static void LogError(this ILogger logger,
        ServerOptions options,
        string message, 
        IEndpointContextBase context)
    {
        var raw = GetRaw(context, options.Logging.SensitiveValuesFilter);
        logger.LogError("{Message}\n{Raw}", message, raw);

        if (options.Logging.UseLogService)
        {
            // TODO: chamar o log sevice
        }
    }

    internal static void LogError(this ILogger logger,
        ServerOptions options,
        string message, 
        string? details, 
        IEndpointContextBase context)
    {
        var raw = GetRaw(context, options.Logging.SensitiveValuesFilter);
        logger.LogError("{Message}: {Details}\n{Raw}", message, details, raw);

        if (options.Logging.UseLogService)
        {
            // TODO: chamar o log sevice
        }
    }

    private static string GetRaw(IEndpointContextBase context, ICollection<string> sensitiveValuesFilter)
    {
        var dict = context.Raw.ToScrubbedDictionary(sensitiveValuesFilter);

        if (context.HttpContext.TraceIdentifier is not null)
            dict["request_id"] = context.HttpContext.TraceIdentifier;

        dict["endpoint"] = context.HttpContext.Request.Method
            + context.HttpContext.Request.Host.ToString()
            + context.HttpContext.Request.Path.ToString();

        if (context is IWithClient clientContext && clientContext.Client is not null)
        {
            dict["client_id"] = clientContext.Client.Id;
            dict["client_name"] = clientContext.Client.Name;
        }

        return Serialize(dict);
    }
}
