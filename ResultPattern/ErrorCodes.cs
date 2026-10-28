namespace LinkRadar.Api.ResultPattern;

// کدهای ثابت خطا؛ مشتری API با کد تصمیم می‌گیرد نه با متن پیام
public static class ErrorCodes
{
    public static class User
    {
        public const string NotFound = "user.not_found";

        // کلید غلط و کاربر غیرفعال یک کد دارند تا معلوم نشود کدام کلید وجود دارد
        public const string InvalidApiKey = "user.invalid_api_key";

        public const string NameRequired = "user.name_required";
        public const string NameTooLong = "user.name_too_long";
        public const string AlreadyExists = "user.already_exists";
    }

    public static class Process
    {
        public const string StepNotFound = "process.step_not_found";
        public const string StepException = "process.step_exception";
    }

    public static class DataForSeo
    {
        public const string HttpError = "dataforseo.http_error";
        public const string StatusError = "dataforseo.status_error";
        public const string TaskError = "dataforseo.task_error";
        public const string InvalidResponse = "dataforseo.invalid_response";
        public const string RequestFailed = "dataforseo.request_failed";
    }
}
