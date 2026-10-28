namespace LinkRadar.Api.Dtos.DataForSeo;

// خروجی هر فراخوانی؛ در خطا هم پر می‌شود تا درخواست و پاسخ خام قابل ذخیره باشند
public class DataForSeoCallResult<TResult>
{
    public string Endpoint { get; set; } = null!;

    public string RequestJson { get; set; } = null!;

    public string? ResponseJson { get; set; }

    public int? HttpStatusCode { get; set; }

    // status_code خود task (20000 = موفق)
    public int? StatusCode { get; set; }

    public string? StatusMessage { get; set; }

    public decimal Cost { get; set; }

    public List<TResult> Results { get; set; } = [];

    // بیشتر endpointها فقط یک result دارند
    public TResult? Result => Results.FirstOrDefault();
}
