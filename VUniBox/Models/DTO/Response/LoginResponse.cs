namespace VUniBox.Models.DTO.Response
{
    public class LoginResponse
    {
        public int UserId { get; set; }
        public string Token { get; set; }
        public DateTime? TokenExpiresAt { get; set; } 
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }
}
