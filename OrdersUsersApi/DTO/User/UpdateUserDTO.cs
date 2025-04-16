namespace OrdersUsersApi.DTO.User
{
    public class UpdateUserDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int cashbackPercent { get; set; }
        public string OldPassword { get; set; } = string.Empty;
        public string? NewPassword { get; set; }
    }
}
