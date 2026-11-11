using aseeMIS.Areas.Common.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace aseeMIS.Areas.Common.ViewComponents
{
    [ViewComponent(Name = "SearchAutocomplete")]
    public class SearchAutocompleteViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(SearchAutocompleteOptions options)
        {
            options ??= new SearchAutocompleteOptions();

            // Basic validation
            if (string.IsNullOrWhiteSpace(options.SearchUrl))
                throw new ArgumentException("SearchAutocomplete: SearchUrl is required.", nameof(options.SearchUrl));

            // Id
            if (string.IsNullOrWhiteSpace(options.Id))
                options.Id = $"ac_{Guid.NewGuid():N}".Substring(0, 10);

            // Defaults
            if (options.MinLength <= 0) options.MinLength = 3;
            if (options.DebounceDelay <= 0) options.DebounceDelay = 400;
            if (options.SuggestionCount <= 0) options.SuggestionCount = 10;

            // Fields (value/text)
            if (string.IsNullOrWhiteSpace(options.ValueField)) options.ValueField = "value";
            if (string.IsNullOrWhiteSpace(options.TextField)) options.TextField = "text";

            // Syncfusion renders the filtering callback as a bare JavaScript
            // identifier. Form/session based control IDs can contain '-' or
            // other characters that are valid in HTML IDs but invalid in a
            // JavaScript function name, causing the entire EJ2 initialization
            // script to fail with "Invalid or unexpected token".
            var safeFunctionSuffix = new string(options.Id
                .Select(character => char.IsLetterOrDigit(character) || character == '_'
                    ? character
                    : '_')
                .ToArray());

            options.FilteringFn = $"onFilter_{safeFunctionSuffix}";

            return View("/Areas/Common/Views/Shared/Components/SearchAutocomplete/Default.cshtml", options);
        }
    }

}
