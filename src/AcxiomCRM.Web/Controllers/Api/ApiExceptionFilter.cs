using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AcxiomCRM.Web.Controllers.Api;

/// <summary>
/// Translates service-layer exceptions into the standard API error envelope:
/// { "error": { "code": "...", "message": "...", "details": { ... } } }.
/// Applied to every API controller via <see cref="CrmApiBaseController"/>.
/// </summary>
public sealed class ApiExceptionFilter : Attribute, IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (statusCode, error) = context.Exception switch
        {
            CrmValidationException ex => (400, ApiErrorDto.Create(
                "validation_failed",
                ex.Message,
                ex.Errors
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Field) ? "general" : e.Field)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray() as object))),

            CrmNotFoundException ex => (404, ApiErrorDto.Create("not_found", ex.Message)),

            UnauthorizedAccessException ex => (403, ApiErrorDto.Create("forbidden", ex.Message)),

            _ => (500, ApiErrorDto.Create("internal_error", "An unexpected error occurred."))
        };

        context.Result = new ObjectResult(error) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
