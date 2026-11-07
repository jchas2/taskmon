namespace Task.Monitor.System;

[Flags]
public enum DrawingCapabilities
{
    // Note: the older OS terminals often draw straight from the font, which can leave line artefacts when 
    // drawing filled rectangles. This enum stores some drawing capabilities based on terminal detection.
    None                = 0,
    QuadrantGlyphs      = 1 << 0,       // Corner glyphs.
    SeamlessBlockGlyphs = 1 << 1,       // Partial and full block glyphs.
    All                 = QuadrantGlyphs | SeamlessBlockGlyphs
}
