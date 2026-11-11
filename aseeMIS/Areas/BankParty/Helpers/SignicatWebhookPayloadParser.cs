using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using dsBankParty.Core.ViewModels;

namespace aseeMIS.Areas.BankParty.Helpers;

public sealed class SignicatWebhookPayloadView
{
    public bool IsVideoConsolidated { get; init; }
    public bool HasReadableSummary { get; init; }
    public IReadOnlyList<SignicatPayloadField> SubjectFields { get; init; } = [];
    public IReadOnlyList<SignicatPayloadField> DocumentFields { get; init; } = [];
    public IReadOnlyList<SignicatSecurityCheckField> SecurityChecks { get; init; } = [];
    public IReadOnlyList<SignicatPayloadField> ProcessFields { get; init; } = [];
}

public sealed record SignicatPayloadField(string Key, string Label, string Value);

public sealed record SignicatSecurityCheckField(string Label, bool? IsValid, string Value);

public sealed record SignicatBankPartyCompareRow(
    string Label,
    string SignicatValue,
    string? BankPartyValue,
    bool HasBankPartyMapping,
    bool IsMatch);

public sealed record SignicatDocumentCopyModel(
    string? DocumentType,
    string? DocumentNumber,
    string? DocumentIssuer,
    string? IssueDate,
    string? ExpiryDate);

public static class SignicatWebhookPayloadParser
{
    private static readonly string[] SubjectKeys =
    [
        "firstName", "givenName", "primaryName", "lastName", "surname", "familyName", "secondaryName", "birthName",
        "middleName", "secondLastName", "secondSurname",
        "dateOfBirth", "birthDate", "gender", "sex",
        "nationality", "nationalityCode",
        "personalNumber", "personalIdentificationNumber", "identificationNumber",
        "placeOfBirth", "birthPlace", "address"
    ];

    private static readonly string[] DocumentKeys =
    [
        "type", "documentType", "documentNumber", "classification",
        "issuer", "issuingCountry", "issuingAuthority",
        "issuingDate", "issueDate", "expiryDate", "dateOfExpiry"
    ];

    private static readonly string[] ProcessKeys =
    [
        "status", "idType", "completion"
    ];

    private static readonly HashSet<string> SkipDocumentKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "issuingDateTS", "expiryDateTS", "issueDateTS", "dateOfExpiryTS"
    };

    private static readonly HashSet<string> BinaryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "photo", "front", "back", "hash", "image", "face", "signature"
    };

    public static SignicatWebhookPayloadView Parse(string? payloadJson, string? eventType)
    {
        var isVideoConsolidated = IsVideoConsolidated(eventType);
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new SignicatWebhookPayloadView { IsVideoConsolidated = isVideoConsolidated };
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            var data = GetProperty(root, "data") ?? root;

            var subject = FindObject(data, "subject") ?? FindObject(root, "subject");
            var document = FindObject(data, "document") ?? FindObject(root, "document");
            var ocr = FindObject(data, "ocr")
                ?? FindObject(document, "ocr")
                ?? FindObject(FindObject(data, "providerSpecific"), "ocr")
                ?? FindObject(FindObject(root, "providerSpecific"), "ocr");
            var videoId = FindObject(data, "videoId") ?? FindObject(root, "videoId");
            var process = FindObject(videoId, "process") ?? FindObject(data, "process");
            var securityChecks = FindObject(data, "securityChecks")
                ?? FindObject(ocr, "securityChecks")
                ?? FindObject(document, "securityChecks")
                ?? FindObject(process, "securityChecks")
                ?? FindObject(root, "securityChecks");

            var subjectFields = CollectFields(subject, SubjectKeys);
            if (subjectFields.Count == 0)
                subjectFields = CollectFields(ocr, SubjectKeys);

            var documentFields = CollectFields(document, DocumentKeys);
            if (documentFields.Count == 0)
                documentFields = CollectFields(ocr, DocumentKeys);

            var processFields = CollectFields(process, ProcessKeys);
            var checks = CollectSecurityChecks(securityChecks);

            return new SignicatWebhookPayloadView
            {
                IsVideoConsolidated = isVideoConsolidated,
                HasReadableSummary = subjectFields.Count > 0 || documentFields.Count > 0 || checks.Count > 0,
                SubjectFields = subjectFields,
                DocumentFields = documentFields,
                SecurityChecks = checks,
                ProcessFields = processFields
            };
        }
        catch (JsonException)
        {
            return new SignicatWebhookPayloadView { IsVideoConsolidated = isVideoConsolidated };
        }
    }

    public static bool IsVideoConsolidated(string? eventType)
        => NormalizeEventType(eventType) == "video-consolidated";

    public static bool IsVideoCompleted(string? eventType)
        => NormalizeEventType(eventType) == "video-completed";

    public static string NormalizeEventType(string? eventType)
        => (eventType ?? string.Empty).Trim().Replace('.', '-').ToLowerInvariant();

    public static SignicatBankPartyCompareRow CompareToBankParty(
        SignicatPayloadField field,
        BankPartyIndividualViewModel? party,
        bool showCrossCheck)
    {
        if (!showCrossCheck || party is null || !TryMapBankPartyValue(field.Key, party, out var bankValue))
            return new(field.Label, field.Value, null, false, true);

        var signicatValue = IsDateKey(field.Key) ? FormatDateDisplay(field.Value) : field.Value;
        var bankDisplay = IsDateKey(field.Key) ? FormatDateDisplay(bankValue) : bankValue;

        return new(
            field.Label,
            signicatValue,
            bankDisplay ?? string.Empty,
            true,
            ValuesEqual(field.Key, field.Value, bankValue, party));
    }

    private static bool TryMapBankPartyValue(string key, BankPartyIndividualViewModel party, out string? value)
    {
        value = key.ToLowerInvariant() switch
        {
            "firstname" or "givenname" or "primaryname" => party.FirstName,
            "lastname" or "surname" or "familyname" or "secondaryname" => party.LastName,
            "dateofbirth" or "birthdate" => party.DateOfBirth.ToString("yyyy-MM-dd"),
            "gender" or "sex" => party.GenderId switch
            {
                1 => string.IsNullOrWhiteSpace(party.GenderName) ? "M" : party.GenderName,
                2 => string.IsNullOrWhiteSpace(party.GenderName) ? "F" : party.GenderName,
                _ => party.GenderName
            },
            "nationality" or "nationalitycode" or "issuingcountry" => party.BankParty?.NationalityCountryCode,
            "personalnumber" or "personalidentificationnumber" or "identificationnumber"
                => party.BankParty?.IdentificationNumber,
            "documentnumber" => party.DocumentNumber,
            "issuer" or "issuingauthority" => party.DocumentIssuer,
            "issuingdate" or "issuedate" => party.DocumentIssueDate?.ToString("yyyy-MM-dd"),
            "expirydate" or "dateofexpiry" => party.DocumentMaturityDate?.ToString("yyyy-MM-dd"),
            "type" or "documenttype" => party.DocumentTypeCode,
            "placeofbirth" or "birthplace" => party.BirthMunicipalityName,
            _ => null
        };

        return value is not null
            || key.ToLowerInvariant() is
                "firstname" or "givenname" or "primaryname" or "lastname" or "surname" or "familyname" or "secondaryname"
                or "dateofbirth" or "birthdate" or "gender" or "sex"
                or "nationality" or "nationalitycode" or "issuingcountry"
                or "personalnumber" or "personalidentificationnumber" or "identificationnumber"
                or "documentnumber" or "issuer" or "issuingauthority"
                or "issuingdate" or "issuedate" or "expirydate" or "dateofexpiry"
                or "type" or "documenttype" or "placeofbirth" or "birthplace";
    }

    private static bool ValuesEqual(
        string key,
        string? signicat,
        string? bank,
        BankPartyIndividualViewModel party)
    {
        if (string.IsNullOrWhiteSpace(signicat) && string.IsNullOrWhiteSpace(bank))
            return true;

        var normalizedKey = key.ToLowerInvariant();
        if (IsDateKey(normalizedKey))
            return DatesEqual(signicat, bank);

        if (normalizedKey is "gender" or "sex")
            return GendersEqual(signicat, bank, party.GenderId);

        if (normalizedKey is "personalnumber" or "personalidentificationnumber" or "identificationnumber" or "documentnumber")
            return IdsEqual(signicat, bank);

        if (normalizedKey is "nationality" or "nationalitycode" or "issuingcountry")
        {
            return CountriesEqual(signicat, bank)
                || CountriesEqual(signicat, party.BankParty?.NationalityCountryName);
        }

        if (normalizedKey is "type" or "documenttype")
            return TextsEqual(MapDocumentType(signicat), bank) || TextsEqual(signicat, bank);

        return TextsEqual(signicat, bank);
    }

    private static bool IsDateKey(string key)
    {
        var normalizedKey = key.ToLowerInvariant();
        return normalizedKey is "dateofbirth" or "birthdate"
            or "issuingdate" or "issuedate"
            or "expirydate" or "dateofexpiry";
    }

    private static bool TextsEqual(string? left, string? right)
        => string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.Ordinal);

    private static string NormalizeText(string? value)
    {
        var text = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        return Regex.Replace(text, @"\s+", " ");
    }

    private static bool IdsEqual(string? left, string? right)
    {
        var leftDigits = DigitsOnly(left);
        var rightDigits = DigitsOnly(right);
        if (leftDigits.Length > 0 && rightDigits.Length > 0)
            return leftDigits == rightDigits;

        return TextsEqual(left, right);
    }

    private static string DigitsOnly(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private static bool CountriesEqual(string? left, string? right)
        => string.Equals(NormalizeCountry(left), NormalizeCountry(right), StringComparison.Ordinal);

    private static string NormalizeCountry(string? value)
    {
        var text = Regex.Replace(NormalizeText(value), @"[^a-z0-9]", string.Empty);
        return text switch
        {
            "rks" or "xk" or "xkx" or "kos" or "kv" or "kosovo" or "kosove" or "kosovë" or "780" => "xk",
            _ => text
        };
    }

    private static bool GendersEqual(string? signicat, string? bank, int genderId)
    {
        var signicatCode = ToGenderId(signicat);
        var bankCode = ToGenderId(bank) ?? (genderId is 1 or 2 ? genderId : null);
        if (signicatCode is not null && bankCode is not null)
            return signicatCode == bankCode;

        return TextsEqual(signicat, bank);
    }

    /// <summary>
    /// Signicat M = BankParty GenderId 1 (mashkull), Signicat F = BankParty GenderId 2 (femer).
    /// </summary>
    private static int? ToGenderId(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text))
            return null;

        if (text is "m" or "male" or "1" or "mashkull" or "man")
            return 1;
        if (text is "f" or "female" or "2" or "femer" or "femër" or "woman")
            return 2;
        if (text.StartsWith("mal") || text.StartsWith("mash"))
            return 1;
        if (text.StartsWith("fem") || text.StartsWith("wom"))
            return 2;

        return int.TryParse(text, out var id) && id is 1 or 2 ? id : null;
    }

    private static bool DatesEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            return true;

        var leftDates = ParseDates(left);
        var rightDates = ParseDates(right);
        if (leftDates.Count == 0 || rightDates.Count == 0)
            return TextsEqual(left, right);

        return leftDates.Any(rightDates.Contains);
    }

    private static string FormatDateDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        var preferred = PreferDate(ParseDates(value));
        return preferred?.ToString("yyyy-MM-dd") ?? value.Trim();
    }

    private static DateOnly? PreferDate(List<DateOnly> dates)
    {
        if (dates.Count == 0)
            return null;
        if (dates.Count == 1)
            return dates[0];

        var today = DateOnly.FromDateTime(DateTime.Today);
        var maxFuture = today.AddYears(20);
        var inWindow = dates
            .Where(d => d <= maxFuture)
            .OrderByDescending(d => d)
            .ToList();

        return inWindow.Count > 0 ? inWindow[0] : dates.Max();
    }

    private static List<DateOnly> ParseDates(string? value)
    {
        var dates = new List<DateOnly>();
        if (string.IsNullOrWhiteSpace(value))
            return dates;

        var text = value.Trim();
        if (text.Contains('T', StringComparison.OrdinalIgnoreCase))
            text = text.Split('T')[0];

        var formats = new[]
        {
            "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy", "dd/MM/yyyy",
            "yyyyMMdd", "yyMMdd", "ddMMyyyy", "ddMMyy", "MM/dd/yyyy",
            "yy-MM-dd", "dd-MM-yy", "dd.MM.yy", "dd/MM/yy"
        };

        foreach (var format in formats)
        {
            if (DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                AddTwoDigitYearVariants(dates, format, exact);
                dates.Add(exact);
            }
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            dates.Add(parsed);

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            dates.Add(DateOnly.FromDateTime(dateTime));

        if (DateTime.TryParse(text, new CultureInfo("en-GB"), DateTimeStyles.None, out var ukDate))
            dates.Add(DateOnly.FromDateTime(ukDate));

        if (Regex.IsMatch(text, @"^\d{6}$")
            && int.TryParse(text[..2], out var year)
            && int.TryParse(text[2..4], out var month)
            && int.TryParse(text[4..6], out var day))
        {
            TryAddDate(dates, 2000 + year, month, day);
            TryAddDate(dates, 1900 + year, month, day);
        }

        return dates.Distinct().ToList();
    }

    public static SignicatDocumentCopyModel GetDocumentCopy(SignicatWebhookPayloadView view)
    {
        static string? Find(IReadOnlyList<SignicatPayloadField> fields, params string[] keys)
            => fields.FirstOrDefault(field => keys.Any(key => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)))?.Value;

        var issuer = Find(view.DocumentFields, "issuingAuthority")
            ?? Find(view.DocumentFields, "issuer");

        return new SignicatDocumentCopyModel(
            MapDocumentType(Find(view.DocumentFields, "type", "documentType")),
            Find(view.DocumentFields, "documentNumber"),
            issuer,
            FormatDateDisplay(Find(view.DocumentFields, "issuingDate", "issueDate")),
            FormatDateDisplay(Find(view.DocumentFields, "expiryDate", "dateOfExpiry")));
    }

    public static string? MapDocumentType(string? signicatType)
    {
        if (string.IsNullOrWhiteSpace(signicatType))
            return signicatType;

        var normalized = signicatType.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
        return normalized switch
        {
            "TD1" or "TD2" or "ID" or "IDCARD" or "IDENTITYCARD" or "NATIONALID" => "IDENTIFICATION_CARD",
            "TD3" or "P" or "PASSPORT" => "PASSPORT",
            _ => signicatType.Trim()
        };
    }

    private static void AddTwoDigitYearVariants(List<DateOnly> dates, string format, DateOnly parsed)
    {
        if (!format.Contains("yy", StringComparison.Ordinal) || format.Contains("yyyy", StringComparison.Ordinal))
            return;

        TryAddDate(dates, parsed.Year % 100 + 1900, parsed.Month, parsed.Day);
        TryAddDate(dates, parsed.Year % 100 + 2000, parsed.Month, parsed.Day);
    }

    private static void TryAddDate(List<DateOnly> dates, int year, int month, int day)
    {
        if (DateOnly.TryParse($"{year:0000}-{month:00}-{day:00}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            dates.Add(date);
    }

    private static List<SignicatPayloadField> CollectFields(JsonElement? source, string[] preferredKeys)
    {
        var fields = new List<SignicatPayloadField>();
        if (source is not { ValueKind: JsonValueKind.Object } obj)
            return fields;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in preferredKeys)
        {
            var value = ReadScalar(GetProperty(obj, key));
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(key))
                continue;
            fields.Add(new SignicatPayloadField(key, ToLabel(key), value));
        }

        foreach (var property in obj.EnumerateObject())
        {
            if (BinaryKeys.Contains(property.Name)
                || SkipDocumentKeys.Contains(property.Name)
                || property.Name.EndsWith("TS", StringComparison.OrdinalIgnoreCase)
                || !seen.Add(property.Name))
                continue;
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                continue;
            var value = ReadScalar(property.Value);
            if (string.IsNullOrWhiteSpace(value))
                continue;
            fields.Add(new SignicatPayloadField(property.Name, ToLabel(property.Name), value));
        }

        return fields;
    }

    private static List<SignicatSecurityCheckField> CollectSecurityChecks(JsonElement? source)
    {
        var checks = new List<SignicatSecurityCheckField>();
        if (source is not { ValueKind: JsonValueKind.Object } obj)
            return checks;

        foreach (var property in obj.EnumerateObject())
        {
            var isValid = ReadCheckValidity(property.Value);
            var value = isValid switch
            {
                true => "Passed",
                false => "Failed",
                _ => ReadScalar(property.Value) ?? "Unknown"
            };

            checks.Add(new SignicatSecurityCheckField(ToLabel(property.Name), isValid, value));
        }

        return checks;
    }

    private static JsonElement? FindObject(JsonElement? source, string name, int depth = 0)
    {
        if (depth > 6 || source is not { ValueKind: JsonValueKind.Object } obj)
            return null;

        var direct = GetProperty(obj, name);
        if (direct is { ValueKind: JsonValueKind.Object })
            return direct;

        foreach (var property in obj.EnumerateObject())
        {
            if (BinaryKeys.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.Object)
                continue;
            var nested = FindObject(property.Value, name, depth + 1);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static JsonElement? GetProperty(JsonElement? source, string name)
    {
        if (source is not { ValueKind: JsonValueKind.Object } obj)
            return null;

        foreach (var property in obj.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }

    private static bool? ReadCheckValidity(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number != 0;

        if (value.ValueKind == JsonValueKind.String)
            return ParseValidityText(value.GetString());

        if (value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in new[] { "valid", "passed", "ok", "success", "result", "status", "check", "livenessDetected" })
        {
            var nested = GetProperty(value, key);
            if (nested is null)
                continue;

            if (nested.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return nested.Value.GetBoolean();
            if (nested.Value.ValueKind == JsonValueKind.String)
            {
                var parsed = ParseValidityText(nested.Value.GetString());
                if (parsed is not null)
                    return parsed;
            }
            if (nested.Value.ValueKind == JsonValueKind.Number && nested.Value.TryGetInt32(out var nestedNumber))
                return nestedNumber != 0;
        }

        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return property.Value.GetBoolean();
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var parsed = ParseValidityText(property.Value.GetString());
                if (parsed is not null)
                    return parsed;
            }
        }

        return null;
    }

    private static bool? ParseValidityText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "ok" or "passed" or "pass" or "success" or "valid" or "accepted" => true,
            "false" or "no" or "ko" or "failed" or "fail" or "invalid" or "rejected" => false,
            _ => null
        };
    }

    private static string? ReadScalar(JsonElement? value)
    {
        if (value is null)
            return null;

        return value.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number => value.Value.ToString(),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static string ToLabel(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var chars = new char[key.Length * 2];
        var length = 0;
        for (var i = 0; i < key.Length; i++)
        {
            var current = key[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(key[i - 1]))
                chars[length++] = ' ';
            chars[length++] = current;
        }

        var text = new string(chars, 0, length).Replace('_', ' ').Trim();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
    }
}
