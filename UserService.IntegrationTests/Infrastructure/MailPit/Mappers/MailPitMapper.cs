using UserService.IntegrationTests.Infrastructure.MailPit.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UserService.IntegrationTests.Infrastructure.MailPit.Mappers
{
    public static class MailPitMapper
    {
        public static EmailDto ToEmailDto(this MailPitMessage message)
        {
            return new EmailDto
            {
                Sender = new EmailContact()
                {
                    Name = message.From.Name,
                    EmailAddress = message.From.Address
                },
                Recipients = message.To.ToEmailContactList(),
                Subject = message.Subject,
                Text = message.Text
            };
        }

        public static List<EmailDto> ToEmailDtoList(this MailPitResponse response)
        {
            return response.Messages
                .Select(m => m.ToEmailDto())
                .ToList();
        }

        public static List<EmailContact> ToEmailContactList(this List<MailPitAddress> mailPitAddressList)
        {
            var emailContactList = new List<EmailContact>();

            foreach (var contact in mailPitAddressList) 
            {
                emailContactList.Add(contact.ToEmailContact());
            }

            return emailContactList;
        }
        public static EmailContact ToEmailContact(this MailPitAddress mailPitAddress)
        {
            return new EmailContact()
            {
                EmailAddress = mailPitAddress.Address,
                Name = mailPitAddress.Name
            };
        }
    }
}
