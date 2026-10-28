namespace LinkRadar.Api.ResultPattern;

public class OperationResult
{
    protected OperationResult(bool isSuccess, List<Error> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public List<Error> Errors { get; }

    // نوع اولین خطا؛ برای انتخاب status code در endpoint
    public ErrorType? ErrorType => Errors.FirstOrDefault()?.Type;

    // فقط وقتی همه‌ی خطاها گذرا باشند، retry معنا دارد
    public bool IsTransient => Errors.Count > 0 && Errors.All(e => e.IsTransient);

    public static OperationResult SuccessResult() => new(true, []);

    public static OperationResult FailureResult(Error error) => new(false, [error]);

    public static OperationResult FailureResult(IEnumerable<Error> errors) => new(false, errors.ToList());

    public string GetErrorMessage()
        => string.Join(Environment.NewLine, Errors.Select(e => $"{e.Code}: {e.Message}"));
}

public class OperationResult<TResult> : OperationResult
{
    private OperationResult(bool isSuccess, TResult? result, List<Error> errors) : base(isSuccess, errors)
    {
        Result = result;
    }

    public TResult? Result { get; }

    public static OperationResult<TResult> SuccessResult(TResult result) => new(true, result, []);

    // result در خطا هم قابل ارسال است، مثلاً پاسخ خام برای ذخیره
    public static OperationResult<TResult> FailureResult(Error error, TResult? result = default)
        => new(false, result, [error]);

    public static OperationResult<TResult> FailureResult(IEnumerable<Error> errors, TResult? result = default)
        => new(false, result, errors.ToList());
}
