using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace VUniBox.Services.Authentication
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Lấy thông tin email và mật khẩu từ appsettings.json
            var senderEmail = _configuration["Email:Email"];
            var senderPassword = _configuration["Email:Password"];

            // Xác thực định dạng email người nhận
            try
            {
                var validatedEmail = new MailAddress(email);
            }
            catch
            {
                throw new FormatException("Email has wrong format: " + email);
            }

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "VUniBox"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                HeadersEncoding = Encoding.UTF8
            };

            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}
