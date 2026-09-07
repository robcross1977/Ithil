using AwesomeAssertions;
using System.Text;

namespace Ithil.Privacy.Tests;

public class PrivacyFilterTests
{
    private static PrivacyFilterService CreateService(List<PiiRule>? customRules = null) =>
        new(new PrivacyFilterOptions { CustomRules = customRules ?? [] });

    private static MemoryStream ToStream(string text)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public async Task Scrub_RedactsEmail()
    {
        var result = await CreateService().ScrubAsync(ToStream("Contact us at user@company.com for help"), TestContext.Current.CancellationToken);
        result.Should().Be("Contact us at [EMAIL REDACTED] for help");
    }

    [Fact]
    public async Task Scrub_RedactsMultipleEmails()
    {
        var result = await CreateService().ScrubAsync(ToStream("From a@x.com to b@y.com"), TestContext.Current.CancellationToken);
        result.Should().NotContain("@");
        result.Should().Contain("[EMAIL REDACTED]");
    }

    [Fact]
    public async Task Scrub_RedactsSsn()
    {
        var result = await CreateService().ScrubAsync(ToStream("SSN: 123-45-6789"), TestContext.Current.CancellationToken);
        result.Should().Be("SSN: [SSN REDACTED]");
    }

    [Fact]
    public async Task Scrub_RedactsCreditCard_WithSpaces()
    {
        var result = await CreateService().ScrubAsync(ToStream("Card: 4111 1111 1111 1111"), TestContext.Current.CancellationToken);
        result.Should().Be("Card: [CARD REDACTED]");
    }

    [Fact]
    public async Task Scrub_RedactsCreditCard_WithDashes()
    {
        var result = await CreateService().ScrubAsync(ToStream("Card: 4111-1111-1111-1111"), TestContext.Current.CancellationToken);
        result.Should().Be("Card: [CARD REDACTED]");
    }

    [Fact]
    public async Task Scrub_RedactsCreditCard_NoSeparator()
    {
        var result = await CreateService().ScrubAsync(ToStream("Card: 4111111111111111"), TestContext.Current.CancellationToken);
        result.Should().Be("Card: [CARD REDACTED]");
    }

    [Fact]
    public async Task Scrub_PassesThrough_ContentWithNoPii()
    {
        var input = """{ "quantity": 42, "sku": "WIDGET-001" }""";
        var result = await CreateService().ScrubAsync(ToStream(input), TestContext.Current.CancellationToken);
        result.Should().Be(input);
    }

    [Fact]
    public async Task Scrub_AppliesCustomRule()
    {
        var rules = new List<PiiRule> { new("ACME-\\d{6}", "[ACCOUNT REDACTED]") };
        var result = await CreateService(rules).ScrubAsync(ToStream("Account: ACME-123456"), TestContext.Current.CancellationToken);
        result.Should().Be("Account: [ACCOUNT REDACTED]");
    }

    [Fact]
    public async Task Scrub_EmptyStream_ReturnsEmptyString()
    {
        var result = await CreateService().ScrubAsync(new MemoryStream(), TestContext.Current.CancellationToken);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Scrub_HandlesEmptyBody_WithoutThrowing()
    {
        var act = async () => await CreateService().ScrubAsync(Stream.Null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Scrub_RedactsAllBuiltInTypes_InSingleString()
    {
        // All three built-in rules must fire on the same content in a single pass.
        var input = "Email: user@corp.com, SSN: 123-45-6789, Card: 4111111111111111";
        var result = await CreateService().ScrubAsync(ToStream(input), TestContext.Current.CancellationToken);

        result.Should().Contain("[EMAIL REDACTED]");
        result.Should().Contain("[SSN REDACTED]");
        result.Should().Contain("[CARD REDACTED]");
        result.Should().NotContain("@");
        result.Should().NotContain("123-45-6789");
        result.Should().NotContain("4111111111111111");
    }

    [Fact]
    public async Task Scrub_AppliesMultipleCustomRules_InSequence()
    {
        // Custom rules are applied in list order after all built-in rules.
        var rules = new List<PiiRule>
        {
            new("ACME-\\d{6}", "[ACCOUNT REDACTED]"),
            new("REF-\\d{4}", "[REF REDACTED]"),
        };
        var input = "Account: ACME-123456, Reference: REF-9999";
        var result = await CreateService(rules).ScrubAsync(ToStream(input), TestContext.Current.CancellationToken);

        result.Should().Be("Account: [ACCOUNT REDACTED], Reference: [REF REDACTED]");
    }

    [Fact]
    public void PiiRule_ExposesPattern_AndReplacement()
    {
        var rule = new PiiRule("\\d{5}", "[ZIP REDACTED]");

        rule.Pattern.Should().Be("\\d{5}");
        rule.Replacement.Should().Be("[ZIP REDACTED]");
    }

    [Fact]
    public async Task PiiRule_Apply_ReplacesAllMatches()
    {
        var rule = new PiiRule("\\d{5}", "[ZIP REDACTED]");
        var result = rule.Apply("Zips: 12345 and 67890");
        result.Should().Be("Zips: [ZIP REDACTED] and [ZIP REDACTED]");
    }
}
