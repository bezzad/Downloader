using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Downloader;

/// <summary>
/// Represents a class for making HTTP requests and handling response headers.
/// </summary>
public class Request
{
    /// <summary>
    /// Gets the configuration for the request.
    /// </summary>
    public readonly RequestConfiguration Configuration;

    /// <summary>
    /// Gets the URI address of the request.
    /// </summary>
    public Uri Address { get; set; }

    /// <summary>
    /// Gets or sets the file name extracted from the URL.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class with the specified address.
    /// </summary>
    /// <param name="address">The URL address to create the request for.</param>
    public Request(string address) : this(address, new RequestConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class with the specified address and configuration.
    /// </summary>
    /// <param name="address">The URL address to create the request for.</param>
    /// <param name="config">The configuration for the request.</param>
    public Request(string address, RequestConfiguration config)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out Uri uri) == false)
        {
            uri = new Uri(new Uri("http://localhost"), address);
        }

        Address = uri;
        Configuration = config ?? new RequestConfiguration();
    }

    /// <summary>
    /// Creates an HTTP request with the specified method.
    /// </summary>
    /// <returns>An instance of <see cref="HttpRequestMessage"/> representing the HTTP request.</returns>
    public HttpRequestMessage GetRequest()
    {
        HttpRequestMessage request = new(HttpMethod.Get, Address);
        request.Version = Configuration.ProtocolVersion;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        request.Headers.IfModifiedSince = Configuration.IfModifiedSince;

        // Handle authentication
        if (Configuration.Authorization != null)
        {
            request.Headers.Authorization = Configuration.Authorization;
        }
        else if (Configuration.Credentials != null)
        {
            if (Configuration.Credentials is NetworkCredential networkCredential)
            {
                if (!string.IsNullOrWhiteSpace(networkCredential.UserName) &&
                    !string.IsNullOrWhiteSpace(networkCredential.Password))
                {
                    request.Headers.Authorization = GetAuthHeader(networkCredential.UserName, networkCredential.Password);
                }
            }
            else if (Configuration.Credentials is CredentialCache credentialCache)
            {
                // For CredentialCache, we need to get the credentials for the current URI
                NetworkCredential creds = credentialCache.GetCredential(Address, "Basic");
                if (creds != null)
                {
                    request.Headers.Authorization = GetAuthHeader(creds.UserName, creds.Password);
                }
            }
        }

        return request;
    }

    private static AuthenticationHeaderValue GetAuthHeader(string user, string password)
    {
        return new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));
    }

    /// <summary>
    /// Gets the file name from the URL.
    /// </summary>
    /// <returns>The file name extracted from the URL.</returns>
    public string GetFileNameFromUrl()
    {
        string filename = Path.GetFileName(Address.LocalPath);
        int queryIndex = filename.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            filename = filename[..queryIndex];
        }

        return filename;
    }

    /// <summary>
    /// Converts the specified text from 'latin-1' encoding to 'utf-8' encoding.
    /// </summary>
    /// <param name="otherEncodedText">The text to convert.</param>
    /// <returns>The converted text in 'utf-8' encoding.</returns>
    public string ToUnicode(string otherEncodedText)
    {
        // decode 'latin-1' to 'utf-8'
        return Encoding.UTF8.GetString(Encoding.GetEncoding("iso-8859-1").GetBytes(otherEncodedText));
    }
}