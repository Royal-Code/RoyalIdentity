using Microsoft.Extensions.Logging;
using RoyalIdentity.Options;
using System.Text.Json.Serialization;
using System.Text.Json;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Withs;

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
    public static string Serialize(object logObject)
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

    internal static void LogError(this ILogger logger,
        ServerOptions options,
        Exception ex,
        string message,
        IEndpointContextBase context)
    {
        var raw = GetRaw(context, options.Logging.SensitiveValuesFilter);
        logger.LogError(ex, "{Message}\n{Raw}", message, raw);

        if (options.Logging.UseLogService)
        {
            // TODO: chamar o log sevice
        }
    }

    internal static void LogError(this ILogger logger,
        IEndpointContextBase context,
        string message,
        string? details = null)
    {
        var options = context.Items.GetOrCreate<ServerOptions>();
        if (details.IsPresent())
            logger.LogError(options, message, details, context);
        else
            logger.LogError(options, message, context);
    }

    internal static void LogError(this ILogger logger,
        IEndpointContextBase context,
        Exception ex,
        string message)
    {
        var options = context.Items.GetOrCreate<ServerOptions>();
        logger.LogError(options, ex, message, context);
    }

    private static string GetRaw(IEndpointContextBase context, ICollection<string> sensitiveValuesFilter)
    {
        var dict = context.Raw.ToScrubbedDictionary(sensitiveValuesFilter);

        if (context.HttpContext.TraceIdentifier is not null)
            dict["request_id"] = context.HttpContext.TraceIdentifier;

        dict["endpoint"] = context.HttpContext.Request.Method
            + context.HttpContext.Request.Host.ToString()
            + context.HttpContext.Request.Path.ToString();

        if (context is IWithClient clientContext && clientContext.ClientParameters.Client is not null)
        {
            dict["client_id"] = clientContext.ClientParameters.Client.Id;
            dict["client_name"] = clientContext.ClientParameters.Client.Name;
        }

        return Serialize(dict);
    }
}
