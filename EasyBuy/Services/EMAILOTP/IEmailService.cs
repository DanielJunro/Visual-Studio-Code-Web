﻿namespace EasyBuy.Services.EMAILOTP
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }

}
