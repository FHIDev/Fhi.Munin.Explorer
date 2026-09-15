using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The hero row on its own: the shape it emits, and what it does with a fact the catalogue has
/// left half filled in.
/// </summary>
/// <remarks>
/// The three detail views are what actually fill it, and their own tests are where the choice of
/// facts and the wording against the sections below are pinned. What is only pinned here is the
/// markup contract a host writes a rule against — one wrapper per fact, so a grid keeps a label
/// with its value — and the two cases that have no value to assert in a view: a note that is
/// absent rather than empty, and a value in one language beside a note in the other.
/// </remarks>
public class DetailFactsTest : BunitContext
{
    private IRenderedComponent<DetailFacts> Render(params DetailFact[] facts) =>
        Render<DetailFacts>(parameters => parameters.Add(p => p.Facts, facts));

    private static IElement Cell(IRenderedComponent<DetailFacts> cut, string label) =>
        cut.FindAll("dl.munin-explorer-page__facts > div")
           .FirstOrDefault(cell => cell.QuerySelector("dt")?.TextContent == label)
        ?? throw new InvalidOperationException($"No '{label}' cell in the row.");

    [Fact]
    public void Facts_WhenAViewNamesSix_ThenEachIsOneWrapperHoldingOneLabelAndOneValue()
    {
        // The wrapper is the load-bearing part of this markup: the list is laid out as a grid, and
        // a grid puts every child in a track of its own — so a dt and a dd written as siblings of
        // the dl land in two separate cells, and the sixth label sits under the fifth value.
        var cut = Render(
            new DetailFact("Type", "Kvalitetsregister"),
            new DetailFact("Dataansvarlig", "St. Olavs hospital HF"),
            new DetailFact("Personidentifikasjon", "Indirekte identifiserbar"),
            new DetailFact("Dataperiode", "2021 – Pågående"),
            new DetailFact("Omfang", "630"),
            new DetailFact("Lovverk", "Forskrift § 2-3"));

        var cells = cut.FindAll("dl.munin-explorer-page__facts > div");

        Assert.Equal(6, cells.Count);
        Assert.All(cells, cell =>
        {
            Assert.Single(cell.QuerySelectorAll("dt"));
            Assert.Single(cell.QuerySelectorAll("dd"));
        });

        Assert.Equal(["Type", "Dataansvarlig", "Personidentifikasjon", "Dataperiode", "Omfang", "Lovverk"],
                     cells.Select(cell => cell.QuerySelector("dt")!.TextContent));
        Assert.Equal("630", Cell(cut, "Omfang").QuerySelector("dd")!.TextContent);
    }

    [Fact]
    public void Note_WhenAFactHasNoneToAdd_ThenNoEmptyElementIsDrawnForIt()
    {
        // An empty <small> is not free: Stiler gives it `display: block` and a margin, so every
        // note-less cell would carry a blank line the cells beside it do not.
        var cut = Render(
            new DetailFact("Type", "Kvalitetsregister"),
            new DetailFact("Omfang", "630", Note: "i 6 datasamlinger"),
            // Whitespace counts as no note, and a label with nothing to name goes with it: the
            // views pass a field the catalogue may not have filled in, and a lone separator —
            // "Gyldighet: " over nothing — is what a label drawn on its own would leave behind.
            new DetailFact("Tilgang", "Ikke-offentlig", NoteLabel: "Gyldighet", Note: "   "));

        Assert.Null(Cell(cut, "Type").QuerySelector("small"));
        Assert.Null(Cell(cut, "Tilgang").QuerySelector("small"));
        Assert.Equal("i 6 datasamlinger", Assert.Single(cut.FindAll("small")).TextContent);
    }

    [Fact]
    public void Value_WhenTheCatalogueHasNotFilledOneIn_ThenTheFactIsDroppedRatherThanDrawnBlank()
    {
        // A dt over an empty dd reads as a value that failed to draw, which is the reading the fact
        // lists below the fold avoid the same way.
        var cut = Render(
            new DetailFact("Type", "Kvalitetsregister"),
            new DetailFact("Dataansvarlig", null),
            new DetailFact("Lovverk", "  "));

        Assert.Equal(["Type"], cut.FindAll("dt").Select(e => e.TextContent));
    }

    [Fact]
    public void Row_WhenNoFactHasAValue_ThenNoListIsDrawnAtAll()
    {
        // Stiler rules the list with a border above and below and 22px of padding between them, so
        // an empty one is two lines across the page under the name block.
        Assert.Empty(Render(new DetailFact("Type", null)).FindAll("dl"));
        Assert.Empty(Render().FindAll("dl"));
    }

    [Fact]
    public void Lang_WhenTheValueIsTheCataloguesNorwegianAndTheNoteIsNot_ThenEachIsMarkedApart()
    {
        // The reason the record carries two marks rather than one. A mark on the <dd> would cover
        // the note as well, and a note is usually this component's own words joined to a count —
        // English for an English reader, and read out in Norwegian phonetics if it inherits.
        var cut = Render(new DetailFact("Data controller", "St. Olavs hospital HF", "no",
                                        NoteLabel: "Valid from", Note: "2010"));

        var value = Cell(cut, "Data controller").QuerySelector("dd")!;
        var marked = value.QuerySelector("span")!;

        Assert.Equal("no", marked.GetAttribute("lang"));
        Assert.Equal("St. Olavs hospital HF", marked.TextContent);
        Assert.False(value.HasAttribute("lang"));

        var note = value.QuerySelector("small")!;

        Assert.Equal("Valid from: 2010", note.TextContent);
        Assert.False(note.HasAttribute("lang"));
        Assert.Empty(note.QuerySelectorAll("span"));
    }

    [Fact]
    public void Lang_WhenTheNoteIsTheCataloguesNorwegianToo_ThenItsValueCarriesAMarkOfItsOwn()
    {
        // The other half of the same record, and the half a lang on the <dd> could not express: a
        // note whose substance is catalogue free text is marked while the value beside it is the
        // reader's own language, which is the datasamling page's count and its telleenhet.
        var value = Cell(Render(new DetailFact("Number of variables", "99", NoteLabel: "Counting unit",
                                               Note: "Pasient", NoteLang: "no")),
                         "Number of variables").QuerySelector("dd")!;
        var note = value.QuerySelector("small")!;

        // The unit alone. NoteLabel is this package's own word, translated into the reader's
        // language by Texts — a mark covering the line would have "Counting unit" announced in
        // Norwegian phonetics, which is the WCAG 3.1.2 failure the mark exists to avoid.
        Assert.Equal("Counting unit: Pasient", note.TextContent);
        Assert.False(note.HasAttribute("lang"));
        Assert.Equal("no", Assert.Single(note.QuerySelectorAll("span")).GetAttribute("lang"));
        Assert.Equal("Pasient", note.QuerySelector("span")!.TextContent);

        // And the count above it is in no language at all, so it is wrapped in nothing.
        Assert.False(value.HasAttribute("lang"));
        Assert.Equal("99", value.ChildNodes.OfType<IText>().Single().TextContent);
    }

    [Fact]
    public void Note_WhenItNeedsNoNaming_ThenTheValueStandsOnItsOwnWithNoSeparator()
    {
        // The source page's collection count, which reads as what it is without a label — so the
        // ": " is a function of NoteLabel rather than a fixture of the second line.
        var cut = Render(new DetailFact("Totalt antall variabler", "630", Note: "i 6 datasamlinger"));

        Assert.Equal("i 6 datasamlinger", Assert.Single(cut.FindAll("small")).TextContent);
    }

    [Fact]
    public void Lang_WhenTheValueIsAlreadyInTheReadersLanguage_ThenNoSpanIsWrappedRoundIt()
    {
        // Unmarked text inherits the host's own language, which is what a Norwegian reader wants
        // for every one of these — an element per value would be markup with nothing to say.
        var value = Cell(Render(new DetailFact("Type", "Kvalitetsregister")), "Type").QuerySelector("dd")!;

        Assert.Empty(value.QuerySelectorAll("span"));
        Assert.Equal("Kvalitetsregister", value.TextContent);
    }
}
