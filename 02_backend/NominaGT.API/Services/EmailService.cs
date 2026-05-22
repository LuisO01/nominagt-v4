using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NominaGT.API.Services;

/// <summary>
/// Envia correos via SMTP usando la configuracion del bloque "Email:Smtp" de appsettings.
/// </summary>
public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config; _log = log;
    }

    public bool EstaConfigurado()
    {
        var host = _config["Email:Smtp:Host"];
        var user = _config["Email:Smtp:Username"];
        return !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(user);
    }

    public record Adjunto(string FileName, byte[] Contenido, string MimeType);

    /// <summary>
    /// Envia un correo con cuerpo HTML y opcionalmente adjuntos.
    /// </summary>
    public async Task EnviarAsync(string para, string asunto, string cuerpoHtml,
        IEnumerable<Adjunto>? adjuntos = null)
    {
        var host = _config["Email:Smtp:Host"];
        var portStr = _config["Email:Smtp:Port"] ?? "587";
        var user = _config["Email:Smtp:Username"];
        var pass = _config["Email:Smtp:Password"];
        var fromName = _config["Email:Smtp:FromName"] ?? "NominaGT";
        var fromAddr = _config["Email:Smtp:FromAddress"] ?? user ?? "no-reply@nominagt.local";
        var useStartTls = (_config["Email:Smtp:UseStartTls"] ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
            throw new InvalidOperationException(
                "SMTP no esta configurado. Define Email:Smtp:Host, Username y Password en appsettings.json.");
        if (string.IsNullOrWhiteSpace(para))
            throw new InvalidOperationException("Falta el destinatario del correo.");
        if (!int.TryParse(portStr, out var port)) port = 587;

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, fromAddr));
        foreach (var to in para.Split(';', ',').Select(s => s.Trim()).Where(s => s.Length > 0))
            msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = asunto;

        var builder = new BodyBuilder { HtmlBody = cuerpoHtml };
        if (adjuntos != null)
        {
            foreach (var a in adjuntos)
                builder.Attachments.Add(a.FileName, a.Contenido, ContentType.Parse(a.MimeType));
        }
        msg.Body = builder.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            // STARTTLS para puerto 587, SSL implicito para 465
            var secure = useStartTls
                ? SecureSocketOptions.StartTls
                : (port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None);
            await smtp.ConnectAsync(host, port, secure);
            await smtp.AuthenticateAsync(user, pass);
            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
            _log.LogInformation("Email enviado a {Para}, asunto={Asunto}", para, asunto);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Error enviando email a {Para}", para);
            throw new InvalidOperationException(
                $"No se pudo enviar el correo: {ex.Message}", ex);
        }
    }

    /// <summary>Plantilla HTML reutilizable con header corporativo.</summary>
    public static string PlantillaHtml(string titulo, string mensaje, string? listaArchivos = null)
    {
        var archivos = string.IsNullOrEmpty(listaArchivos) ? ""
            : $"<p style='color:#64748b;font-size:13px;margin-top:24px'>Archivos adjuntos: <strong>{listaArchivos}</strong></p>";
        return $@"
<div style=""font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:600px;margin:0 auto;background:#f8fafc;padding:0"">
  <div style=""background:#0f172a;color:#f8fafc;padding:24px;text-align:center"">
    <h1 style=""margin:0;font-size:22px"">NominaGT v4</h1>
    <p style=""margin:6px 0 0;color:#94a3b8;font-size:13px"">Sistema empresarial de nómina</p>
  </div>
  <div style=""padding:32px 24px;background:#ffffff"">
    <h2 style=""margin:0 0 16px;color:#0f172a;font-size:18px"">{titulo}</h2>
    <div style=""color:#334155;font-size:14px;line-height:1.6"">{mensaje}</div>
    {archivos}
  </div>
  <div style=""background:#0f172a;color:#94a3b8;padding:16px;text-align:center;font-size:11px"">
    Este es un correo automático. No respondas a este mensaje.
  </div>
</div>";
    }
}
