using System.ComponentModel.DataAnnotations;

namespace GuessWord.Contracts.Requests
{
    public class RegisterRequestDto
    {
        [Required]
        [MaxLength(64, ErrorMessage = "Допустимо не более 64 символов.")]
        public string Login { get; set; } = null!;
        [Required]
        [MaxLength(1024, ErrorMessage = "Допустимо не более 1024 символов.")]
        public string Password { get; set; } = null!;
        [Required]
        [MaxLength(64, ErrorMessage = "Допустимо не более 64 символов.")]
        public string Name { get; set; } = null!;
    }
}
