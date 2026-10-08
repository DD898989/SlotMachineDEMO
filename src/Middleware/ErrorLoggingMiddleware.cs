using ServerMain.Api;

namespace ServerMain.Middleware;

public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly object _lock = new();

    public ErrorLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            if (context.Response.StatusCode != 200)
            {
                WriteLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            WriteLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> 500 Exception: {"¿ù»~"/*{ex}*/}");

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new ErrorResponse { What = ex.Message });
            }
        }
    }

    private static void WriteLog(string message)
    {
        lock (_lock)
        {
            File.AppendAllText("error.log", message + Environment.NewLine);
        }
    }
}
