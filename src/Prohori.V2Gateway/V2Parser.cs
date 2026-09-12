using NHapi.Base.Parser;
using NHapi.Base.Util;

namespace Prohori.V2Gateway;

/// <summary>Parses raw ER7 (pipe-delimited HL7 v2) text into an <see cref="AdtMessage"/>, via
/// NHapi's <see cref="Terser"/> — a generic segment/field accessor that works across HL7 v2
/// versions without needing the version-specific typed message classes NHapi also ships.</summary>
public static class V2Parser
{
    public static AdtMessage Parse(string er7)
    {
        NHapi.Base.Model.IMessage message;
        try
        {
            message = new PipeParser().Parse(er7);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new FormatException($"Could not parse as HL7 v2 ER7: {ex.Message}", ex);
        }
        var terser = new Terser(message);

        string? Get(string path)
        {
            try
            {
                var value = terser.Get(path);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (Exception) { return null; }
        }

        return new AdtMessage
        {
            TriggerEvent = Get("/MSH-9-2") ?? throw new FormatException("Missing MSH-9-2 (trigger event)."),
            Mrn = Get("/PID-3-1") ?? throw new FormatException("Missing PID-3-1 (MRN)."),
            FamilyName = Get("/PID-5-1") ?? throw new FormatException("Missing PID-5-1 (family name)."),
            GivenNames = Get("/PID-5-2") is { } given ? [given] : [],
            Gender = Get("/PID-8-1"),
            BirthDate = Get("/PID-7-1"),
            City = Get("/PID-11-3"),
            District = Get("/PID-11-4"),
            VisitNumber = Get("/PV1-19-1"),
            EncounterClass = Get("/PV1-2-1"),
            AdmitDateTime = ParseV2DateTime(Get("/PV1-44-1")),
            DischargeDateTime = ParseV2DateTime(Get("/PV1-45-1")),
        };
    }

    /// <summary>HL7 v2 TS: YYYYMMDDHHMM[SS] with no timezone — treated as local Bangladesh time,
    /// same assumption CaseBundleBuilder's synthetic visit dates make.</summary>
    private static DateTimeOffset? ParseV2DateTime(string? v2)
    {
        if (string.IsNullOrEmpty(v2)) return null;
        var digits = v2.Length >= 12 ? v2[..12] : v2.PadRight(12, '0');
        return DateTimeOffset.TryParseExact(digits, "yyyyMMddHHmm",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? new DateTimeOffset(parsed.DateTime, TimeSpan.FromHours(6))
            : null;
    }
}
