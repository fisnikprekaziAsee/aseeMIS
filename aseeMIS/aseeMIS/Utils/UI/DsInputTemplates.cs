using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace aseeMIS.Utils.UI
{
    public static class DsInputTemplates
    {
        public static IHtmlContent Icon(string iconCss, string? title = null)
        {
            var icon = Encode(iconCss);
            var titleAttribute = string.IsNullOrWhiteSpace(title)
                ? string.Empty
                : $" title='{Encode(title)}'";

            return new HtmlString(
                $"<span class='e-input-group-icon'{titleAttribute}><i class='{icon}'></i></span>" +
                "<span class='e-input-separator'></span>");
        }

        public static IHtmlContent PersonIcon() => Icon("bi bi-person");

        public static IHtmlContent FullNameIcon() => Icon("bi bi-person-vcard");

        public static IHtmlContent PhoneIcon() => Icon("bi bi-phone");

        public static IHtmlContent WorkPhoneIcon() => Icon("bi bi-telephone");

        public static IHtmlContent ViberIcon() => Icon("bi bi-chat-dots");

        public static IHtmlContent WhatsAppIcon() => Icon("bi bi-whatsapp");

        public static IHtmlContent EmailIcon() => Icon("bi bi-envelope");

        public static IHtmlContent PhonePrefix(
            string badgeId,
            string prefix = "+383",
            string iconCss = "bi bi-phone",
            string title = "Phone prefix")
        {
            var safeBadgeId = Encode(badgeId);
            var safePrefix = Encode(prefix);
            var icon = Encode(iconCss);
            var safeTitle = Encode(title);

            return new HtmlString(
                $"<span id='{safeBadgeId}' class='e-input-group-icon ds-phone-prefix' title='{safeTitle}'>" +
                $"<i class='{icon}'></i><span class='ds-prefix-text'>{safePrefix}</span></span>" +
                "<span class='e-input-separator'></span>");
        }

        public static IHtmlContent EditablePhonePrefix(
            string badgeId,
            string inputId,
            string prefix = "+",
            string iconCss = "bi bi-phone",
            string title = "Phone prefix")
        {
            var safeBadgeId = Encode(badgeId);
            var safeInputId = Encode(inputId);
            var safePrefix = Encode(prefix);
            var icon = Encode(iconCss);
            var safeTitle = Encode(title);

            return new HtmlString(
                $"<span id='{safeBadgeId}' class='e-input-group-icon ds-phone-prefix ds-phone-prefix-editable is-resident' title='{safeTitle}'>" +
                $"<i class='{icon}'></i><input id='{safeInputId}' class='ds-prefix-input' type='text' value='{safePrefix}' maxlength='6' inputmode='tel' aria-label='{safeTitle}' />" +
                "</span><span class='e-input-separator'></span>");
        }

        public static IHtmlContent CopyFrom(
            string targetId,
            string sourceId,
            string iconCss = "bi bi-arrow-down-circle",
            string title = "Copy")
        {
            return CopyButton(sourceId, targetId, "field", iconCss, title);
        }

        public static IHtmlContent CopyToClipboard(
            string sourceId,
            string iconCss = "bi bi-copy",
            string title = "Copy")
        {
            return CopyButton(sourceId, null, "clipboard", iconCss, title);
        }

        public static IHtmlContent CopyPhoneToClipboard(
            string sourceId,
            string prefixInputId,
            string iconCss = "bi bi-copy",
            string title = "Copy")
        {
            var prefix = Encode(prefixInputId);
            return CopyButton(sourceId, null, "clipboard", iconCss, title, $" data-ds-copy-prefix='{prefix}'");
        }

        public static IHtmlContent CopyPhoneFrom(
            string targetId,
            string sourceId,
            string targetPrefixInputId,
            string sourcePrefixInputId,
            string iconCss = "bi bi-arrow-down-circle",
            string title = "Copy")
        {
            var sourcePrefix = Encode(sourcePrefixInputId);
            var targetPrefix = Encode(targetPrefixInputId);

            return CopyButton(
                sourceId,
                targetId,
                "field",
                iconCss,
                title,
                $" data-ds-copy-source-prefix='{sourcePrefix}' data-ds-copy-target-prefix='{targetPrefix}'");
        }

        public static IHtmlContent CopyCombinedToClipboard(
            IEnumerable<string> sourceIds,
            string separator = " ",
            string iconCss = "bi bi-copy",
            string title = "Copy")
        {
            var sources = Encode(string.Join(",", sourceIds ?? Array.Empty<string>()));
            var safeSeparator = Encode(separator);
            return CopyButton(
                string.Empty,
                null,
                "clipboard",
                iconCss,
                title,
                $" data-ds-copy-sources='{sources}' data-ds-copy-separator='{safeSeparator}'");
        }

        public static IHtmlContent EmailDomain(string domain)
        {
            var safeDomain = Encode(domain);

            return new HtmlString(
                $"<span class='e-input-group-icon ds-email-domain-wrapper'>" +
                $"<span class='ds-email-domain'>{safeDomain}</span>" +
                $"</span>");
        }

        private static IHtmlContent CopyButton(
            string sourceId,
            string? targetId,
            string mode,
            string iconCss,
            string title,
            string extraAttributes = "")
        {
            var source = Encode(sourceId);
            var target = Encode(targetId ?? string.Empty);
            var safeMode = Encode(mode);
            var icon = Encode(iconCss);
            var safeTitle = Encode(title);
            var targetAttribute = string.IsNullOrWhiteSpace(target)
                ? string.Empty
                : $" data-ds-copy-target='{target}'";

            return new HtmlString(
                $"<span class='e-input-group-icon ds-copy-btn' " +
                $"role='button' tabindex='0' title='{safeTitle}' aria-label='{safeTitle}' " +
                $"data-ds-copy-source='{source}'{targetAttribute} data-ds-copy-mode='{safeMode}'{extraAttributes}>" +
                $"<i class='{icon}'></i>" +
                $"</span>");
        }

        private static string Encode(string value)
            => HtmlEncoder.Default.Encode(value ?? string.Empty);
    }
}
