using RoyalIdentity.Endpoints.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Items;

/// <summary>
/// Data structure to store issued tokens
/// </summary>
public class Tokens
{
    private readonly Dictionary<string, Token> tokens = [];

    public void Add(string type, string value)
    {
        tokens[type] = new Token(type, value);
    }

    public void Add(Token token)
    {
        tokens[token.TokenType] = token;
    }

    public Token? Get(string type)
    {
        tokens.TryGetValue(type, out var token);
        return token;
    }

    public bool TryGet(string type, [NotNullWhen(true)] out Token? token)
    {
        return tokens.TryGetValue(type, out token);
    }
}

public static class TokensExtensions
{
    public static void AddToken(this ContextItems items, Token token)
    {
        items.GetOrCreate<Tokens>().Add(token);
    }

    public static bool TryGetToken(this ContextItems items, string type, [NotNullWhen(true)] out Token? token)
    {
        return items.GetOrCreate<Tokens>().TryGet(type, out token);
    }
}
