namespace MarikinaMarket.API.Domain.Enums
{
    public enum OtpVerificationResult
    {
        Success,
        InvalidCode,
        Expired,
        TooManyAttempts,
        NotFound
    }
}