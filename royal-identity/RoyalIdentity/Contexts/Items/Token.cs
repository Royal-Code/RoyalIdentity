using RoyalIdentity.Extensions;

namespace RoyalIdentity.Contexts.Items;

/// <summary>
/// Represents an issued token in event payloads without retaining its raw value.
/// </summary>
public class Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="value">The value.</param>
    public Token(string type, string value)
    {
        TokenType = type;
        TokenValue = value.Obfuscate();
    }

    /// <summary>
    /// Gets the type of the token.
    /// </summary>
    /// <value>
    /// The type of the token.
    /// </value>
    public string TokenType { get; }

    /// <summary>
    /// Gets the obfuscated token value safe for event serialization.
    /// </summary>
    /// <value>
    /// The obfuscated token value.
    /// </value>
    public string TokenValue { get; }
}
