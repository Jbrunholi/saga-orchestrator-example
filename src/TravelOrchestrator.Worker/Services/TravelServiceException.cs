using System;
using System.Net;

namespace TravelOrchestrator.Worker.Services;

public class TravelServiceException : Exception
{
    public TravelServiceException(string service, string message, HttpStatusCode statusCode, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Service = service;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public string Service { get; }

    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }
}
