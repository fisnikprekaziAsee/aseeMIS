using System.Globalization;
using System.Reflection;
using System.Text;
using dsMIS.Core.DTOs;
using Syncfusion.EJ2.Base;

namespace aseeMIS.Areas.Common.Utils
{
    /// <summary>
    /// Translates a Syncfusion <see cref="DataManagerRequest"/> (sent by ej.DataManager + UrlAdaptor) into our
    /// generic <see cref="PageRequest"/> so a single backend pipeline (<c>SelectPagedAsync</c>) can serve both
    /// custom callers and grid widgets in RemoteMode.
    /// <para>
    /// Filter values are bound as parameters (@0, @1, ...) instead of being inlined into the predicate string
    /// to avoid quoting/escaping concerns and let EF infer parameter types from the ViewModel.
    /// </para>
    /// </summary>
    public static class DataManagerRequestExtensions
    {
        /// <summary>
        /// Map a Syncfusion grid request into a <see cref="PageRequest"/>.
        /// </summary>
        /// <param name="dm">The Syncfusion data manager request from the grid.</param>
        /// <param name="viewModelType">The grid's row model type — used to discover string properties for the search box.</param>
        /// <param name="defaultOrderBy">Order-by clause applied when the request has no Sorted entries (typically the primary key).</param>
        public static PageRequest ToPageRequest(this DataManagerRequest dm, Type viewModelType, string? defaultOrderBy = null)
        {
            ArgumentNullException.ThrowIfNull(dm);
            ArgumentNullException.ThrowIfNull(viewModelType);

            var skip = dm.Skip < 0 ? 0 : dm.Skip;
            var take = dm.Take <= 0 ? 20 : dm.Take;

            var args = new List<object>();
            var filter = BuildFilter(dm, viewModelType, args);
            var orderBy = BuildOrderBy(dm.Sorted) ?? defaultOrderBy;

            return new PageRequest
            {
                Page = (skip / take) + 1,
                PageSize = take,
                Filter = filter,
                FilterArgs = args.Count > 0 ? args.ToArray() : null,
                OrderBy = orderBy
            };
        }

        // ---------------- Filter ----------------

        private static string? BuildFilter(DataManagerRequest dm, Type viewModelType, List<object> args)
        {
            var parts = new List<string>();

            if (dm.Where is { Count: > 0 })
            {
                foreach (var w in dm.Where)
                {
                    var clause = WhereToString(w, args);
                    if (!string.IsNullOrEmpty(clause))
                        parts.Add($"({clause})");
                }
            }

            if (dm.Search is { Count: > 0 })
            {
                foreach (var s in dm.Search)
                {
                    var clause = SearchToString(s, viewModelType, args);
                    if (!string.IsNullOrEmpty(clause))
                        parts.Add($"({clause})");
                }
            }

            return parts.Count == 0 ? null : string.Join(" and ", parts);
        }

        private static string? WhereToString(WhereFilter w, List<object> args)
        {
            if (w.IsComplex && w.predicates is { Count: > 0 })
            {
                var inner = new List<string>();
                var firstCondition = w.predicates[0]?.Condition;

                foreach (var p in w.predicates)
                {
                    var part = WhereToString(p, args);
                    if (!string.IsNullOrEmpty(part))
                        inner.Add(part);
                }

                if (inner.Count == 0) return null;

                // All siblings share the same Condition under a single complex node in Syncfusion's convention.
                var joiner = NormalizeCondition(firstCondition);
                return string.Join($" {joiner} ", inner.Select(p => $"({p})"));
            }

            if (string.IsNullOrWhiteSpace(w.Field) || string.IsNullOrWhiteSpace(w.Operator))
                return null;

            return BuildLeafPredicate(w, args);
        }

        private static string BuildLeafPredicate(WhereFilter w, List<object> args)
        {
            var field = SanitizeFieldName(w.Field);
            var op = w.Operator?.ToLowerInvariant() ?? "equal";
            var ignoreCase = w.IgnoreCase;

            // For null comparisons we don't need a parameter binding.
            if (w.value is null && (op == "equal" || op == "notequal"))
            {
                var nullOp = op == "equal" ? "==" : "!=";
                return $"{field} {nullOp} null";
            }

            // Bind value as @N — EF infers correct SQL type from the parameter at execution time.
            var paramIndex = args.Count;
            args.Add(w.value!);
            var paramRef = $"@{paramIndex}";

            return op switch
            {
                "equal" => ignoreCase
                    ? $"({field} != null && {field}.ToLower() == {paramRef}.ToString().ToLower())"
                    : $"{field} == {paramRef}",
                "notequal" => ignoreCase
                    ? $"({field} == null || {field}.ToLower() != {paramRef}.ToString().ToLower())"
                    : $"{field} != {paramRef}",
                "lessthan" => $"{field} < {paramRef}",
                "lessthanorequal" => $"{field} <= {paramRef}",
                "greaterthan" => $"{field} > {paramRef}",
                "greaterthanorequal" => $"{field} >= {paramRef}",
                "startswith" => ignoreCase
                    ? $"({field} != null && {field}.ToLower().StartsWith({paramRef}.ToString().ToLower()))"
                    : $"({field} != null && {field}.StartsWith({paramRef}.ToString()))",
                "endswith" => ignoreCase
                    ? $"({field} != null && {field}.ToLower().EndsWith({paramRef}.ToString().ToLower()))"
                    : $"({field} != null && {field}.EndsWith({paramRef}.ToString()))",
                "contains" => ignoreCase
                    ? $"({field} != null && {field}.ToLower().Contains({paramRef}.ToString().ToLower()))"
                    : $"({field} != null && {field}.Contains({paramRef}.ToString()))",
                "doesnotcontain" => ignoreCase
                    ? $"({field} == null || !{field}.ToLower().Contains({paramRef}.ToString().ToLower()))"
                    : $"({field} == null || !{field}.Contains({paramRef}.ToString()))",
                _ => $"{field} == {paramRef}" // safe fallback
            };
        }

        // ---------------- Search ----------------

        private static string? SearchToString(SearchFilter s, Type viewModelType, List<object> args)
        {
            if (string.IsNullOrWhiteSpace(s.Key)) return null;

            // Caller-provided fields win; otherwise use every string property on the ViewModel (typical free-text search).
            var fields = s.Fields is { Count: > 0 }
                ? s.Fields
                : GetStringPropertyNames(viewModelType);

            if (fields.Count == 0) return null;

            var paramIndex = args.Count;
            args.Add(s.Key);
            var paramRef = $"@{paramIndex}";

            // SearchFilter in Syncfusion v32 doesn't expose IgnoreCase — search is case-insensitive by default
            // server-side because we explicitly lowercase both sides below.
            const bool ignoreCase = true;
            var op = string.IsNullOrWhiteSpace(s.Operator) ? "contains" : s.Operator.ToLowerInvariant();

            var clauses = new List<string>(fields.Count);
            foreach (var raw in fields)
            {
                var field = SanitizeFieldName(raw);
                clauses.Add(BuildStringSearchClause(field, op, paramRef, ignoreCase));
            }

            return string.Join(" or ", clauses);
        }

        private static string BuildStringSearchClause(string field, string op, string paramRef, bool ignoreCase)
        {
            // All string-search operators share the same null-guard + optional .ToLower() shape.
            var fieldExpr = ignoreCase ? $"{field}.ToLower()" : field;
            var valueExpr = ignoreCase ? $"{paramRef}.ToString().ToLower()" : $"{paramRef}.ToString()";

            return op switch
            {
                "startswith" => $"({field} != null && {fieldExpr}.StartsWith({valueExpr}))",
                "endswith" => $"({field} != null && {fieldExpr}.EndsWith({valueExpr}))",
                "equal" => $"({field} != null && {fieldExpr} == {valueExpr})",
                _ => $"({field} != null && {fieldExpr}.Contains({valueExpr}))"
            };
        }

        // ---------------- Sort ----------------

        private static string? BuildOrderBy(List<Sort>? sorted)
        {
            if (sorted is null || sorted.Count == 0) return null;

            var sb = new StringBuilder();
            var first = true;
            foreach (var s in sorted)
            {
                if (string.IsNullOrWhiteSpace(s?.Name)) continue;

                if (!first) sb.Append(", ");
                sb.Append(SanitizeFieldName(s.Name));

                var dir = s.Direction?.ToLowerInvariant();
                sb.Append(dir is "descending" or "desc" ? " DESC" : " ASC");
                first = false;
            }

            return sb.Length == 0 ? null : sb.ToString();
        }

        // ---------------- Helpers ----------------

        private static List<string> GetStringPropertyNames(Type type)
        {
            // Cache could be added later if reflection ever shows up in profiles; for now keep it simple
            // because grids are bound once per request and the property set is tiny.
            return type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name)
                .ToList();
        }

        /// <summary>
        /// Defense in depth: even though Dynamic LINQ only evaluates predicates against the entity's compiled
        /// properties, accepting arbitrary identifiers from the client is unwise. Allow only A-Z/0-9/_/. so a
        /// hostile request can't break out of the predicate context.
        /// </summary>
        private static string SanitizeFieldName(string field)
        {
            if (string.IsNullOrEmpty(field)) return field;
            foreach (var c in field)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '.')
                    throw new ArgumentException($"Invalid field name in grid request: '{field}'", nameof(field));
            }
            return field;
        }

        private static string NormalizeCondition(string? condition)
            => string.Equals(condition, "or", StringComparison.OrdinalIgnoreCase) ? "or" : "and";
    }
}
