namespace RoyalIdentity.Contracts
{
    /// <summary>
    /// Represents a secret extracted from the HttpContext
    /// </summary>
    [Redesign("Trocar nome (?SecretChecked?), não necessidade de carregar dados dos segredos, mas sim retornar alguns dados identificando o tipo e resultado da validação")]
    public class ParsedSecret
    {
        /// <summary>
        /// Gets or sets the identifier associated with this secret
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the credential to verify the secret
        /// </summary>
        /// <value>
        /// The credential.
        /// </value>
        public object Credential { get; set; }

        /// <summary>
        /// Gets or sets the type of the secret
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets additional properties.
        /// </summary>
        /// <value>
        /// The properties.
        /// </value>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}