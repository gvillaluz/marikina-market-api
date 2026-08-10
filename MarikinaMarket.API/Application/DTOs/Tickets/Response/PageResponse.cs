namespace MarikinaMarket.API.Application.DTOs.Tickets.Response
{
    public class PageResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public bool HasMore { get; set; }
    }
}