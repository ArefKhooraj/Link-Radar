namespace LinkRadar.Api.Dtos.DataForSeo;

// پوسته‌ی مشترک همه‌ی پاسخ‌های DataForSEO؛ TResult شکل هر آیتم tasks[].result[] است
public class DataForSeoResponse<TResult>
{
    public int StatusCode { get; set; }

    public string? StatusMessage { get; set; }

    public decimal Cost { get; set; }

    public List<DataForSeoTask<TResult>>? Tasks { get; set; }
}

public class DataForSeoTask<TResult>
{
    public string? Id { get; set; }

    public int StatusCode { get; set; }

    public string? StatusMessage { get; set; }

    public decimal Cost { get; set; }

    public string? Time { get; set; }

    public List<TResult>? Result { get; set; }
}
