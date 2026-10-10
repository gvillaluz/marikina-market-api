namespace MarikinaMarket.API.Application.DTOs.User.Response
{
    public class UserNamesResponse
    {
        public int Id { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }
}
