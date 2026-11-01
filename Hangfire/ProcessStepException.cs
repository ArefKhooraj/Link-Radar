using LinkRadar.Api.Entities;

namespace LinkRadar.Api.Hangfire;

// step ناموفق را به Hangfire می‌رساند تا retry کند و در داشبورد Failed نشان دهد
public class ProcessStepException(CurrentStep step, string message)
    : Exception($"Step {step} Failed: {message}");
