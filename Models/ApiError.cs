namespace Capstone.Api.Models;

public sealed class ApiError
{
    public string Message { get; set; }


    public ApiError(string message)
    {
        Message = message;
    }


    public ApiError()
    {
        Message = "";
    }
}
