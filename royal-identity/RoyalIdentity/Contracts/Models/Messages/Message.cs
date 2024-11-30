using System.Text.Json.Serialization;

namespace RoyalIdentity.Contracts.Models.Messages;

/// <summary>
/// Base class for data that needs to be written out as cookies.
/// </summary>
public class Message<TModel>
{
    /// <summary>
    /// Creates a new instance of the <see cref="Message{TModel}"/> class.
    /// </summary>
    /// <param name="data"></param>
    public Message(TModel data) : this(data, DateTime.UtcNow) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Message{TModel}"/> class.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="now">The current UTC date/time.</param>
    public Message(TModel data, DateTime now) : this(data, now.Ticks) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Message{TModel}"/> class.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="created">The ticks the message was created.</param>
    [JsonConstructor]
    public Message(TModel data, long created)
    {
        Data = data;
        Created = created;
    }

    /// <summary>
    /// Gets or sets the UTC ticks the <see cref="Message{TModel}" /> was created.
    /// </summary>
    /// <value>
    /// The created UTC ticks.
    /// </value>
    public long Created { get; set; }

    /// <summary>
    /// Gets or sets the data.
    /// </summary>
    /// <value>
    /// The data.
    /// </value>
    public TModel Data { get; set; }
}