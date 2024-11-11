using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Mail;

namespace PingClient
{
    public enum ValidationError
    {
        None,
        UsernameTooShort,
        UsernameInvalidCharacters,
        PasswordsDoNotMatch,
        PasswordTooShort,
        PasswordInvalidFormat,
        EmailInvalidFormat,
        DatabaseError
    }

    public class Authentication
    {
        private readonly IDatabaseService _databaseService;
        private const int MinUsernameLength = 4;
        private const int MinPasswordLength = 10;

        public Authentication(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<ValidationError> RegisterUser(string username, string email, string password1, string password2)
        {
            ValidationError error = ValidateInput(username, email, password1, password2);
            if (error != ValidationError.None)
            {
                return error;
            }

            string hashedPassword = HashPassword(password1);

            bool isInserted = await _databaseService.InsertUserIntoDatabase(username, email, hashedPassword);
            if (!isInserted)
            {
                return ValidationError.DatabaseError;
            }

            return ValidationError.None;
        }

        private ValidationError ValidateInput(string username, string email, string password1, string password2)
        {
            if (username.Length < MinUsernameLength)
            {
                return ValidationError.UsernameTooShort;
            }

            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            {
                return ValidationError.UsernameInvalidCharacters;
            }

            if (password1 != password2)
            {
                return ValidationError.PasswordsDoNotMatch;
            }

            if (password1.Length < MinPasswordLength)
            {
                return ValidationError.PasswordTooShort;
            }

            // Regex to check for at least one uppercase letter, one number, and one special character
            if (!Regex.IsMatch(password1, @"^(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).+$"))
            {
                return ValidationError.PasswordInvalidFormat;
            }

            if (!IsValidEmail(email))
            {
                return ValidationError.EmailInvalidFormat;
            }

            return ValidationError.None;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                MailAddress m = new MailAddress(email);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

    }
}
