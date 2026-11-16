using System.Security.Cryptography;
using System.Text;


namespace UsersService.Application.UsersService.Services.Helpers
{
    public static class AuthCodeGenerator
    {
        private const string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string GenerateCode(int length = 6)
        {
            if (length <= 0)
                throw new ArgumentException("Length must be greater than 0", nameof(length));

            var chars = AllowedChars.ToCharArray();
            var data = new byte[length];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(chars[b % chars.Length]);
            }

            return result.ToString();
        }
    }
}
