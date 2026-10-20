using Task.Monitor.System.Services.InstalledApps;

namespace Task.Monitor.System.Services.Tests.InstalledApps;

public sealed class CopyrightPublisherTests
{
    [Fact]
    public void Parse_Keeps_A_Trailing_Full_Stop_Only_For_A_Company_Abbreviation()
    {
        Assert.Equal("Acme Ltd.", CopyrightPublisher.Parse("© 2024 Acme Ltd."));
        Assert.Equal("Acme Corporation", CopyrightPublisher.Parse("© 2024 Acme Corporation."));
    }

    [Fact]
    public void Parse_Reads_A_Leading_Year_Without_A_Mark_As_A_Copyright()
    {
        Assert.Equal("Docker Inc.", CopyrightPublisher.Parse("2015-2024 Docker Inc. All rights reserved."));
    }

    [Fact]
    public void Parse_Returns_Null_For_Text_That_Is_Not_A_Copyright_Notice()
    {
        // No copyright mark and no leading year: a description, not a notice naming a developer.
        Assert.Null(CopyrightPublisher.Parse("Version 1.0, built for macOS"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("© 2024")]
    [InlineData("Copyright © 2010-2024. All rights reserved.")]
    public void Parse_Returns_Null_When_No_Developer_Is_Named(string? copyright)
    {
        Assert.Null(CopyrightPublisher.Parse(copyright));
    }

    // Real NSHumanReadableCopyright strings from installed bundles, and the forms they vary in.
    [Theory]
    [InlineData("Copyright © 2004–2023 Apple Inc. All rights reserved.",             "Apple Inc.")]
    [InlineData("© 2003–2023 Apple Inc. All rights reserved.",                       "Apple Inc.")]
    [InlineData("Copyright (C) 2026 Microsoft. All rights reserved",                 "Microsoft")]
    [InlineData("Copyright 1984 -2026 Adobe Systems Incorporated. All rights reserved.", "Adobe Systems Incorporated")]
    [InlineData("Copyright © 2023 Zoom Video Communications, Inc. All rights reserved.", "Zoom Video Communications, Inc.")]
    [InlineData("© 2012–present Spotify AB",                                        "Spotify AB")]
    [InlineData("Copyright (c) 2023, Acme Corp.",                                   "Acme Corp.")]
    [InlineData("Copyright 2009-2024 by Sublime HQ Pty Ltd",                        "Sublime HQ Pty Ltd")]
    [InlineData("©2024 Ghostty",                                                    "Ghostty")]
    public void Parse_Returns_The_Developer_Named_After_The_Mark(string copyright, string expected)
    {
        Assert.Equal(expected, CopyrightPublisher.Parse(copyright));
    }

    [Fact]
    public void Parse_Uses_Only_The_First_Line()
    {
        // Apple's localized strings put "All rights reserved." on a second line, separated from the
        // name by non-breaking spaces.
        Assert.Equal("Apple Inc.", CopyrightPublisher.Parse("Copyright ©\u00a02016–2025 Apple\u00a0Inc.\nAll rights reserved."));
    }

    [Fact]
    public void Parse_Uses_The_Name_Before_The_Mark_When_Nothing_Follows_It()
    {
        Assert.Equal("Acme Inc.", CopyrightPublisher.Parse("Acme Inc. © 2024"));
    }
}
