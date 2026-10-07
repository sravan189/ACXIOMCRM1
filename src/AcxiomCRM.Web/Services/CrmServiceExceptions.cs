namespace AcxiomCRM.Web.Services;

/// <summary>
/// Thrown by services when a server-side business rule is violated.
/// Carries field-keyed errors so controllers can feed them back into ModelState.
/// </summary>
public class CrmValidationException : Exception
{
    public IReadOnlyList<(string Field, string Message)> Errors { get; }

    public CrmValidationException(params (string Field, string Message)[] errors)
        : base(errors.Length > 0 ? errors[0].Message : "Validation failed.")
    {
        Errors = errors;
    }
}

/// <summary>
/// Thrown by services when a record is missing or outside the caller's visibility scope.
/// Controllers translate this into a 404.
/// </summary>
public class CrmNotFoundException : Exception
{
    public CrmNotFoundException(string message) : base(message) { }
}
