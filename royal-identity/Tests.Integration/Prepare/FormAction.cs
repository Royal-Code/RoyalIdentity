using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;

namespace Tests.Integration.Prepare;

/// <summary>
/// Represents a form action to be submitted.
/// </summary>
public class FormAction
{
    private readonly HttpClient client;
    private readonly string action;
    private readonly string method;
    private readonly List<KeyValuePair<string, string>> content;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormAction"/> class.
    /// </summary>
    /// <param name="client">The HTTP client.</param>
    /// <param name="form">The form element.</param>
    public FormAction(HttpClient client, HtmlNode form)
    {
        this.client = client;

        action = form.GetAttributeValue("action", "");
        method = form.GetAttributeValue("method", HttpMethods.Post);

        content = [];

        // query all input, textarea, select elements
        var inputs = form.SelectNodes("//input | //textarea | //select");
        // for each element, get the name and value and add to content
        foreach (var input in inputs)
        {
            var name = input.GetAttributeValue("name", "");
            var value = input.GetAttributeValue("value", "");
            content.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    /// <summary>
    /// Sets the value of the specified input element.
    /// </summary>
    /// <param name="name">The name of the input element.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>
    ///     The current <see cref="FormAction"/> instance.
    /// </returns>
    public FormAction SetValue(string name, string value)
    {
        var index = content.FindIndex(x => x.Key == name);
        if (index >= 0)
        {
            content[index] = new KeyValuePair<string, string>(name, value);
        }
        else
        {
            throw new InvalidOperationException($"Element with name '{name}' not found.");
        }

        return this;
    }

    /// <summary>
    /// Adds a new key-value pair to the form content.
    /// </summary>
    /// <param name="name">The name of the input element.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>
    ///     The current <see cref="FormAction"/> instance.
    /// </returns>
    public FormAction AddValue(string name, string value)
    {
        content.Add(new KeyValuePair<string, string>(name, value));
        return this;
    }

    /// <summary>
    /// Submits the form action.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation, 
    ///     which when completed will return the HTTP response message.
    /// </returns>
    public async Task<HttpResponseMessage> SubmitAsync()
    {
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), action)
        {
            Content = new FormUrlEncodedContent(content)
        });
        return response;
    }
}
