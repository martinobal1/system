using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsSuas.Infrastructure.Messaging;

public sealed record EmailMessage(string ToAddress, string Subject, string Body, string? FromAddress = null);

public interface IEmailSender
{
    string ProviderName { get; }
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>DEV sink — always succeeds and records as sent.</summary>
public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger) => _logger = logger;

    public string ProviderName => "Null";

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("NullEmailSender → {To}: {Subject}", message.ToAddress, message.Subject);
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string ProviderName => "Smtp";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Email:SmtpHost nie je nastavený.");

        var port = _configuration.GetValue("Email:SmtpPort", 25);
        var from = message.FromAddress
            ?? _configuration["Email:FromAddress"]
            ?? "noreply@issuas.local";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = _configuration.GetValue("Email:SmtpEnableSsl", false),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        var user = _configuration["Email:SmtpUser"];
        var pass = _configuration["Email:SmtpPassword"];
        if (!string.IsNullOrWhiteSpace(user))
            client.Credentials = new NetworkCredential(user, pass);

        using var mail = new MailMessage(from, message.ToAddress, message.Subject, message.Body);
        await client.SendMailAsync(mail, cancellationToken);
        _logger.LogInformation("SMTP e-mail odoslaný na {To}", message.ToAddress);
    }
}

public static class EmailSenderRegistration
{
    public static IServiceCollection AddEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Email:Provider"] ?? "Null";
        if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, NullEmailSender>();
        return services;
    }
}
