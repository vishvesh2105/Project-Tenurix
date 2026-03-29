using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Capstone.Api.Services;

/// <summary>
/// Generates professional lease agreement PDF documents using QuestPDF.
/// </summary>
public sealed class LeaseDocumentService
{
    public LeaseDocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateLeaseAgreement(LeaseDocumentData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginTop(50);
                page.MarginBottom(40);
                page.MarginHorizontal(50);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().Element(c => ComposeContent(c, data));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private void ComposeHeader(IContainer container, LeaseDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().BorderBottom(3).BorderColor(Colors.Blue.Darken3).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("TENURIX").FontSize(28).Bold().FontColor(Colors.Blue.Darken3);
                    c.Item().Text("Property Management").FontSize(10).FontColor(Colors.Grey.Medium).LetterSpacing(0.5f);
                });
                row.ConstantItem(180).AlignRight().Column(c =>
                {
                    c.Item().Text("RESIDENTIAL LEASE").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                    c.Item().Text("AGREEMENT").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                    c.Item().PaddingTop(4).Text($"Lease #{data.LeaseId}").FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text($"Date Issued: {data.IssuedDate:MMMM d, yyyy}").FontSize(9).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignRight().Text($"Document ID: TEN-{data.LeaseId:D6}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeContent(IContainer container, LeaseDocumentData data)
    {
        container.PaddingTop(16).Column(col =>
        {
            col.Spacing(10);

            // ── Preamble
            col.Item().Text(text =>
            {
                text.Span("This Residential Lease Agreement (\"Agreement\") is entered into as of ");
                text.Span($"{data.LeaseStartDate:MMMM d, yyyy}").Bold();
                text.Span(", by and between the following parties:");
            });

            // ── Parties section
            col.Item().PaddingTop(4).Element(c => ComposePartiesSection(c, data));

            // ── Property description
            col.Item().Element(c => SectionHeader(c, "1. PREMISES"));
            col.Item().Text(text =>
            {
                text.Span("The Landlord hereby leases to the Tenant, and the Tenant hereby leases from the Landlord, the residential property located at: ");
                text.Span(data.PropertyAddress).Bold();
                text.Span(" (the \"Premises\"), for use as a private residential dwelling only.");
            });

            // ── Lease Term
            col.Item().Element(c => SectionHeader(c, "2. LEASE TERM"));
            col.Item().Text(text =>
            {
                text.Span("The lease term shall commence on ");
                text.Span($"{data.LeaseStartDate:MMMM d, yyyy}").Bold();
                text.Span(" and shall terminate on ");
                text.Span($"{data.LeaseEndDate:MMMM d, yyyy}").Bold();
                text.Span(", unless renewed or terminated earlier in accordance with the terms of this Agreement.");
            });

            // ── Rent
            col.Item().Element(c => SectionHeader(c, "3. RENT"));
            if (data.RentAmount > 0)
            {
                col.Item().Text(text =>
                {
                    text.Span("The Tenant agrees to pay a monthly rent of ");
                    text.Span($"${data.RentAmount:N2} CAD").Bold();
                    text.Span(", due on the first (1st) day of each calendar month. Rent shall be paid via electronic transfer or any other method agreed upon by both parties.");
                });
            }
            else
            {
                col.Item().Text("Rent amount and payment terms shall be as separately agreed upon by the parties in writing.");
            }

            // ── Security Deposit
            col.Item().Element(c => SectionHeader(c, "4. SECURITY DEPOSIT"));
            col.Item().Text("The Tenant shall provide a security deposit as required by applicable provincial/territorial law. The deposit will be held in accordance with local tenancy regulations and returned within the legally mandated period after the termination of this lease, subject to any lawful deductions for damages beyond normal wear and tear.");

            // ── Maintenance and Repairs
            col.Item().Element(c => SectionHeader(c, "5. MAINTENANCE AND REPAIRS"));
            col.Item().Text("The Tenant shall maintain the Premises in a clean and habitable condition. The Tenant shall promptly notify the Landlord or property management of any maintenance issues or needed repairs through the Tenurix platform. The Landlord shall be responsible for structural repairs and maintenance of essential services (plumbing, electrical, heating) in accordance with applicable housing standards.");

            // ── Use of Premises
            col.Item().Element(c => SectionHeader(c, "6. USE OF PREMISES"));
            col.Item().Text(text =>
            {
                text.Span("The Premises shall be used exclusively as a private residential dwelling by the Tenant and approved occupants (");
                text.Span($"{data.NumberOfOccupants} occupant(s)").Bold();
                text.Span("). The Tenant shall not use the Premises for any unlawful purpose or in any manner that creates a nuisance or disturbance.");
            });

            // ── Pets
            col.Item().Element(c => SectionHeader(c, "7. PET POLICY"));
            if (data.HasPets)
            {
                col.Item().Text(text =>
                {
                    text.Span("Pets are ");
                    text.Span("permitted").Bold();
                    text.Span(" on the Premises, subject to the following conditions: ");
                    text.Span(data.PetDetails ?? "As agreed between parties");
                    text.Span(". The Tenant shall be liable for any damages caused by their pet(s).");
                });
            }
            else
            {
                col.Item().Text("No pets are permitted on the Premises without prior written consent from the Landlord.");
            }

            // ── Utilities
            col.Item().Element(c => SectionHeader(c, "8. UTILITIES"));
            col.Item().Text("Unless otherwise agreed in writing, the Tenant shall be responsible for all utility charges including electricity, gas, water, internet, and cable services for the Premises during the lease term.");

            // ── Entry by Landlord
            col.Item().Element(c => SectionHeader(c, "9. LANDLORD'S RIGHT OF ENTRY"));
            col.Item().Text("The Landlord or property management may enter the Premises for inspections, repairs, or showings upon providing at least 24 hours written notice to the Tenant, except in cases of emergency where immediate entry may be required.");

            // ── Termination
            col.Item().Element(c => SectionHeader(c, "10. EARLY TERMINATION"));
            col.Item().Text("Either party may terminate this Agreement early by providing at least 60 days written notice. The Tenant shall remain responsible for rent through the end of the notice period or until a replacement tenant is secured, whichever comes first.");

            // ── Governing Law
            col.Item().Element(c => SectionHeader(c, "11. GOVERNING LAW"));
            col.Item().Text("This Agreement shall be governed by and construed in accordance with the residential tenancy laws of the applicable province or territory of Canada. All disputes shall be resolved through the appropriate Landlord and Tenant Board or equivalent tribunal.");

            // ── General Provisions
            col.Item().Element(c => SectionHeader(c, "12. GENERAL PROVISIONS"));
            col.Item().Text("This Agreement constitutes the entire understanding between the parties concerning the lease of the Premises. Any amendments or modifications must be in writing and signed by both parties. If any provision of this Agreement is found to be invalid or unenforceable, the remaining provisions shall continue in full force and effect.");

            // ── Signature section
            col.Item().PaddingTop(20).Element(c => ComposeSignatureSection(c, data));

            // ── Footer note
            col.Item().PaddingTop(16).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(8)
                .Text("This lease agreement was generated electronically through the Tenurix Property Management platform. Digital signatures carry the same legal validity as handwritten signatures under applicable electronic commerce legislation.")
                .FontSize(8).FontColor(Colors.Grey.Medium).Italic();
        });
    }

    private void ComposePartiesSection(IContainer container, LeaseDocumentData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(20);
                columns.RelativeColumn();
            });

            // Landlord box
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Blue.Lighten5).Padding(12).Column(c =>
            {
                c.Item().Text("LANDLORD").FontSize(8).Bold().FontColor(Colors.Blue.Darken3).LetterSpacing(1f);
                c.Item().PaddingTop(6).Text(data.LandlordName).FontSize(11).Bold();
                c.Item().PaddingTop(2).Text(data.LandlordEmail).FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            table.Cell(); // spacer

            // Tenant box
            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Green.Lighten5).Padding(12).Column(c =>
            {
                c.Item().Text("TENANT").FontSize(8).Bold().FontColor(Colors.Green.Darken3).LetterSpacing(1f);
                c.Item().PaddingTop(6).Text(data.TenantName).FontSize(11).Bold();
                c.Item().PaddingTop(2).Text(data.TenantEmail).FontSize(9).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(data.TenantPhone))
                    c.Item().PaddingTop(2).Text($"Phone: {data.TenantPhone}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeSignatureSection(IContainer container, LeaseDocumentData data)
    {
        container.Column(col =>
        {
            col.Item().Text("SIGNATURES").FontSize(12).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().PaddingTop(4).Text("By signing below, both parties agree to all terms and conditions outlined in this Agreement.").FontSize(9).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(16).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(40);
                    columns.RelativeColumn();
                });

                // Landlord signature
                table.Cell().Column(c =>
                {
                    c.Item().PaddingBottom(30).Text(""); // space for signature
                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2);
                    c.Item().PaddingTop(4).Text("Landlord Signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text(data.LandlordName).FontSize(10).Bold();
                    c.Item().PaddingTop(2).Text("Date: _______________").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                table.Cell(); // spacer

                // Tenant signature
                table.Cell().Column(c =>
                {
                    if (!string.IsNullOrWhiteSpace(data.TenantSignedAt))
                    {
                        c.Item().PaddingBottom(6).Text("✓ Digitally Signed").FontSize(11).Bold().FontColor(Colors.Green.Darken3);
                        c.Item().Text($"Signed: {data.TenantSignedAt}").FontSize(9).FontColor(Colors.Green.Darken2);
                    }
                    else
                    {
                        c.Item().PaddingBottom(30).Text(""); // space for signature
                    }

                    c.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(2);
                    c.Item().PaddingTop(4).Text("Tenant Signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text(data.TenantName).FontSize(10).Bold();
                    c.Item().PaddingTop(2).Text("Date: _______________").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private void SectionHeader(IContainer container, string title)
    {
        container.PaddingTop(6).Column(col =>
        {
            col.Item().Text(title).FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span("Tenurix Property Management").FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(" | ").FontSize(8).FontColor(Colors.Grey.Lighten1);
                text.Span("support@tenurix.net").FontSize(8).FontColor(Colors.Grey.Medium);
            });
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Page ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                text.Span(" of ").FontSize(8).FontColor(Colors.Grey.Medium);
                text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }
}

/// <summary>
/// All data needed to populate a lease agreement document.
/// </summary>
public sealed class LeaseDocumentData
{
    public int LeaseId { get; set; }
    public DateTime IssuedDate { get; set; } = DateTime.UtcNow;

    // Parties
    public string LandlordName { get; set; } = "";
    public string LandlordEmail { get; set; } = "";
    public string TenantName { get; set; } = "";
    public string TenantEmail { get; set; } = "";
    public string? TenantPhone { get; set; }

    // Property
    public string PropertyAddress { get; set; } = "";

    // Lease terms
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public decimal RentAmount { get; set; }

    // Household
    public int NumberOfOccupants { get; set; } = 1;
    public bool HasPets { get; set; }
    public string? PetDetails { get; set; }

    // Signature
    public string? TenantSignedAt { get; set; }
}
