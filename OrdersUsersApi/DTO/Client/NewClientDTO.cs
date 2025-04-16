namespace OrdersUsersApi.DTO.Client
{
    public class NewClientDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Comment { get; set; }

    }
}
