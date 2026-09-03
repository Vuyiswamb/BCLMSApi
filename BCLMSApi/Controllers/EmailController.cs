namespace TshwaneYouthAPi.Controllers
{ 
    using global::BCLMSApi.Services;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    namespace BCLMSApi.Controllers
    {
        [ApiController]
        [Route("api/[controller]")]
        public class EmailController(IEmailService emailService) : ControllerBase
        {

            [HttpGet("SendTestEmail")]
            public async Task<IActionResult> SendTestEmail(string token)
            {
                try
                {
                    // Verify test token
                    if (token != "tok_8f27ac91b3d54e629c4e0f79")
                    {
                        return Unauthorized(new
                        {
                            Success = false,
                            Message = "Invalid test token",
                            Timestamp = DateTime.UtcNow,
                            RequestIP = GetClientIPAddress(HttpContext)
                        });
                    }

                    // Test configuration
                    string testRecipient = "VuyiswaMa@tshwane.gov.za";
                    string testSubject = "Office 365 - Ithuba Email Test";
                    string testBody = "This is a test email to verify Office 365 email functionality.";

                    // Get client IP for diagnostics
                    string clientIP = GetClientIPAddress(HttpContext);

                    // Add diagnostic info to email body
                    string diagnosticBody = $@"{testBody}

Diagnostic Information:
- Test Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
- Endpoint: /api/Email/SendEmail
- Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not Set"}
- Client IP Address: {clientIP}
- Machine Name: {Environment.MachineName}
- User Agent: {HttpContext.Request.Headers["User-Agent"].ToString()}";

                    // Send test email
                    await emailService.SendEmailOffice365Async(
                        testRecipient,
                        testSubject,
                        diagnosticBody);

                    return Ok(new
                    {
                        Success = true,
                        Message = "Test email sent successfully",
                        Details = new
                        {
                            Recipient = testRecipient,
                            Subject = testSubject,
                            SentAt = DateTime.UtcNow,
                            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                            RequestIP = clientIP,
                            Server = Environment.MachineName
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Log the full exception for debugging
                    // _logger.LogError(ex, "Email test failed");

                    string clientIP = GetClientIPAddress(HttpContext);

                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "Failed to send test email",
                        Error = new
                        {
                            Type = ex.GetType().Name,
                            Message = ex.Message,
                            InnerException = ex.InnerException?.Message,
                            // StackTrace = ex.StackTrace // Only include in development
                        },
                        DiagnosticInfo = new
                        {
                            Timestamp = DateTime.UtcNow,
                            MachineName = Environment.MachineName,
                            Application = "Ithuba API",
                            ClientIP = clientIP,
                            RequestPath = HttpContext.Request.Path,
                            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                        }
                    });
                }
            }


            [HttpGet("SendEmail")]
            public async Task<IActionResult> SendEmail(string token, string To, string Email_address, string Subject,string body   )
            {
                try
                {
                    // Verify test token
                    if (token != "tok_8f27ac91b3d54e629c4e0f79")
                    {
                        return Unauthorized(new
                        {
                            Success = false,
                            Message = "Invalid Email token",
                            Timestamp = DateTime.UtcNow,
                            RequestIP = GetClientIPAddress(HttpContext)
                        });
                    }

                    // Test configuration
                    string testRecipient = To;
                    string testSubject = Subject;
                    string testBody = body;

                    // Get client IP for diagnostics
                    string clientIP = GetClientIPAddress(HttpContext);

                    // Add diagnostic info to email body
                    string diagnosticBody = $@"{testBody}

Diagnostic Information:
- Test Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
- Endpoint: /api/Email/SendEmail
- Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not Set"}
- Client IP Address: {clientIP}
- Machine Name: {Environment.MachineName}
- User Agent: {HttpContext.Request.Headers["User-Agent"].ToString()}";

                    // Send test email
                    await emailService.SendEmailOffice365Async(
                        testRecipient,
                        testSubject,
                        diagnosticBody);

                    return Ok(new
                    {
                        Success = true,
                        Message = "Email sent successfully",
                        Details = new
                        {
                            Recipient = testRecipient,
                            Subject = testSubject,
                            SentAt = DateTime.UtcNow,
                            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                            RequestIP = clientIP,
                            Server = Environment.MachineName
                        }
                    });
                }
                catch (Exception ex)
                {
                    // Log the full exception for debugging
                    // _logger.LogError(ex, "Email test failed");

                    string clientIP = GetClientIPAddress(HttpContext);

                    return StatusCode(500, new
                    {
                        Success = false,
                        Message = "Failed to send test email",
                        Error = new
                        {
                            Type = ex.GetType().Name,
                            Message = ex.Message,
                            InnerException = ex.InnerException?.Message,
                            // StackTrace = ex.StackTrace // Only include in development
                        },
                        DiagnosticInfo = new
                        {
                            Timestamp = DateTime.UtcNow,
                            MachineName = Environment.MachineName,
                            Application = "Ithuba API",
                            ClientIP = clientIP,
                            RequestPath = HttpContext.Request.Path,
                            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString()
                        }
                    });
                }
            }
            // Helper method to get client IP address
            private string GetClientIPAddress(HttpContext httpContext)
            {
                try
                {
                    // Check for forwarded headers (when behind proxy/load balancer)
                    string forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(forwardedFor))
                    {
                        // X-Forwarded-For can contain multiple IPs, take the first one
                        string[] ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        return ips.FirstOrDefault()?.Trim();
                    }

                    // Check for other common proxy headers
                    string realIP = httpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(realIP))
                        return realIP;

                    // Fall back to remote IP address
                    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                }
                catch
                {
                    return "Error retrieving IP";
                }
            }

        }
  
    }
}
