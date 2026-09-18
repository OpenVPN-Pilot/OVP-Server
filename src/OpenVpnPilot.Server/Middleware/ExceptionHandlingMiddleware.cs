using System.Security.Cryptography;
using OpenVpnPilot.Server.Contracts;
using OpenVpnPilot.Server.Services;

namespace OpenVpnPilot.Server.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ServiceException exception)
        {
            PipelineLog.Refused(logger, context.Request.Method, context.Request.Path, exception.Status, exception.Code, exception.Message);
            await ProblemResponses.WriteAsync(
                context, exception.Status, exception.Code, exception.Message, exception.Wipe, exception.Errors);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away. There is nobody left to answer and nothing went wrong here.
            PipelineLog.Aborted(logger, context.Request.Method, context.Request.Path);
        }
        catch (CryptographicException exception)
        {
            PipelineLog.DecryptionFailed(logger, exception);
            await ProblemResponses.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.ServerDataKeyMismatch,
                "Stored data could not be decrypted. The operator has to check the data key.");
        }
        catch (Exception exception)
        {
            PipelineLog.Unhandled(logger, context.Request.Method, context.Request.Path, exception);
            await ProblemResponses.WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.ServerError,
                "The server failed to handle the request. The request id identifies it in the server log.");
        }
    }
}
