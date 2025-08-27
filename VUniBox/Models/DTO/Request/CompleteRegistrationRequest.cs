namespace VUniBox.Models.DTO.Request
{
    public class CompleteRegistrationRequest
    {
        public string Email { get; set; }
        public string OtpToken { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
    }
}
