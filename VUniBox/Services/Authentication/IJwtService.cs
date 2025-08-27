using VUniBox.Models;
using VUniBox.Models.DTO.Response;

namespace VUniBox.Services.Authentication
{
    public interface IJwtService
    {
       TokenResponse GenerateTokens(Users user);
    }
}
