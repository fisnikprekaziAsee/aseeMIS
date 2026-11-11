using dsMIS.Application.DTOs;
using dsMIS.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace aseeMIS.Utils;

// Uses the active controller's MVC validators and ModelState, retaining framework validation semantics.
public sealed class MvcCrudModelValidator(ModelStateDictionary modelState, Func<object, bool> tryValidateModel) : ICrudModelValidator
{
    public CrudValidationResult Validate(object model)
    {
        modelState.Clear();
        var valid = tryValidateModel(model);
        var errors = modelState.Values.SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!).Distinct().ToList();
        return new(valid, modelState.IsValid, errors);
    }
}
