using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
using Task.Monitor.System.Screens;
using SysThreading = System.Threading;

namespace Task.Monitor.System.Controls;

public class Control
{
    // The Collection acts as a proxy for updates to the underlying List<T>.
    // This provides a clean api for interacting with the Collection on the
    // control, similar to the WinForms Controls Collection.
    private readonly ControlCollection controlCollection;

    // The container holding the List<T> for rendering. We don't expose it via a public api.
    private List<Control> controls = [];

    private string? name = null;

    private AnsiScreenBuffer frame = new();

    // Row glyphs are staged in a scratch buffer so a row costs one cursor move and one
    // append instead of one move per cell. Rows wider than this fall back to the heap.
    private const int MaxStackAllocChars = 512;

    private static readonly object drawingLock = new();
    private static int drawingLocksAcquired = 0;
    
    private readonly ISystemTerminal terminal;

    public Control(ISystemTerminal terminal)
    {
        this.terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
        controlCollection = new ControlCollection(this);
    }

    public virtual Color BackgroundColour { get; set; } = ConsolePalette.Black;

    private bool CanFocus => Visible && TabStop;

    public void Clear() => OnClear();
    
    internal void ClearControls() => controls.Clear();

    internal int ControlCount => controls.Count;

    protected void DrawHorizontalLine(
        int y,
        int x1,
        int x2,
        Color colour)
    {
        int width = x2 - x1 + 1;

        if (width < 1) {
            return;
        }

        Span<char> line = width <= MaxStackAllocChars
            ? stackalloc char[width]
            : new char[width];

        line.Fill('\u2500');
        line[0] = '\u2576';

        if (width > 2) {
            line[width - 2] = '\u2574';
        }

        frame.Clear();
        frame.MoveTo(x1, y);
        frame.SetColour(colour, BackgroundColour);
        frame.Append(line);
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
    
    protected void DrawVerticalLine(
        int x, 
        int y1, 
        int y2, 
        Color colour)
    {
        frame.Clear();
        frame.MoveTo(x, y1);
        frame.SetColour(colour, BackgroundColour);

        for (int i = y1; i < y2; i++) {
            frame.MoveTo(x, i);
            
            if (i == y1) {
                frame.Append('\u2577');
                continue;
            }

            if (i + 1 == y2) {
                frame.Append('\u2575');
                continue;
            }
            
            frame.Append('\u2502');
        }
        
        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }
    
    protected static void DrawingLockAcquire()
    {
        drawingLocksAcquired++;
        SysThreading::Monitor.Enter(drawingLock);
    }

    protected static void DrawingLockRelease()
    {
        if (drawingLocksAcquired == 0) {
            return;
        }
        
        drawingLocksAcquired--;
        
        SysThreading::Monitor.Exit(drawingLock);
    }

    protected void DrawRectangle(
        int x, 
        int y, 
        int width,
        int height,
        Color colour)
    {
        if (width < 1 || height < 1) {
            return;
        }

        Span<char> line = width <= MaxStackAllocChars
            ? stackalloc char[width]
            : new char[width];

        line.Fill(' ');

        frame.Clear();
        frame.SetColour(colour, colour);

        for (int i = y; i < y + height; i++) {
            frame.MoveTo(x, i);
            frame.Append(line);
        }

        frame.ResetColour();
        terminal.Write(frame.AsSpan());
    }

    protected void DrawRectangleWithBevel(
        int x, 
        int y, 
        int width,
        int height,
        Color colour)
    {
        if (width < 1 || height < 1) return;

        frame.Clear();

        if (width == 1) {
            frame.SetColour(colour, BackgroundColour);

            for (int row = y; row < y + height; row++) {
                frame.MoveTo(x, row);
                frame.Append('▞');
            }

            frame.ResetColour();
            terminal.Write(frame.AsSpan());
            return;
        }

        int bottom = y + height - 1;

        Span<char> line = width <= MaxStackAllocChars
            ? stackalloc char[width]
            : new char[width];

        line.Fill('▄');
        line[0] = '▗';
        line[width - 1] = '▖';

        frame.MoveTo(x, y);
        frame.SetColour(colour, BackgroundColour);
        frame.Append(line);

        for (int row = y + 1; row < bottom; row++) {
            frame.MoveTo(x, row);
            frame.SetColour(colour, BackgroundColour);
            frame.Append('▐'); // \u2590

            if (width > 2) {
                frame.SetColour(colour, colour);
                frame.Append(' ', width - 2);
            }

            frame.SetColour(colour, BackgroundColour);
            frame.Append('▌'); // \u258C
        }

        if (height > 1) {
            line.Fill('▀');
            line[0] = '▝';
            line[width - 1] = '▘';

            frame.MoveTo(x, bottom);
            frame.SetColour(colour, BackgroundColour);
            frame.Append(line);
        }

        frame.ResetColour();
        terminal.Write(frame.AsSpan());
    }    
    
    public void Draw()
    {
        if (RedrawEnabled && Visible) {
            OnDraw();
        }
    }
    
    internal bool ContainsControl(Control control)
    {
        ArgumentNullException.ThrowIfNull(control, nameof(control));
        
        return controls.Contains(control);
    }
   
    public ControlCollection Controls => controlCollection;

    internal bool Focused { get; set; } = false;
    
    public virtual Color ForegroundColour { get; set; } = ConsolePalette.White;
    
    internal Control GetControlByIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, controls.Count, nameof(index));
        
        return controls[index];
    }

    protected Control? GetFocusedControl => Controls.Where(ctrl => ctrl.Focused).FirstOrDefault();
    
    private IEnumerable<Control> GetOrderedTabControls(ControlCollection controls, bool lookForward)
    {
        var focusableControls = controls
            .Where(ctrl => ctrl.CanFocus);

        return lookForward
            ? focusableControls.OrderBy(ctrl => ctrl.TabIndex)
            : focusableControls.OrderByDescending(ctrl => ctrl.TabIndex);
    }

    internal void GotFocus() => OnGotFocus();

    private Screen? GetParentScreen()
    {
        Control? ctrl = this;

        while (ctrl != null) {
            if (ctrl is Screen screen) {
                return screen;
            }
            ctrl = ctrl.Parent;
        }

        return ctrl as Screen;
    }
    
    public int Height { get; set; } = 0;

    internal int IndexOfControl(Control control)
    {
        for (int i = 0; i < controls.Count; i++) {
            if (controls[i] == control) {
                return i;
            }
        }

        return -1;
    }
    
    internal void InsertControl(int index, Control control)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, controls.Count, nameof(index));

        control.Parent = this;
        controls.Insert(index, control);
    }

    internal void InsertControls(Control[] controls)
    {
        for (int i = 0; i < controls.Length; i++) {
            controls[i].Parent = this;
        }
        
        this.controls.AddRange(controls);
    }
    
    public void KeyPressed(ConsoleKeyInfo keyInfo, ref bool handled) => OnKeyPressed(keyInfo, ref handled);

    public string? Name
    {
        get => name ?? string.Empty;
        set => name = value ?? string.Empty;
    }
    
    public void Load() => OnLoad();

    internal void LostFocus() => OnLostFocus();

    protected virtual void OnClear() =>
        DrawRectangle(
            X, 
            Y, 
            Width, 
            Height, 
            BackgroundColour);

    protected virtual void OnDraw() { }

    protected virtual void OnGotFocus() { }

    protected virtual void OnLoad()
    {
        foreach (Control control in Controls) {
            control.Load();
        }
    }

    protected virtual void OnLostFocus() { }

    protected virtual void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        handled = keyInfo.Key switch {
            ConsoleKey.Tab when keyInfo.Modifiers == ConsoleModifiers.None => ProcessTabKey(lookForward: true),
            ConsoleKey.Tab when keyInfo.Modifiers == ConsoleModifiers.Shift => ProcessTabKey(lookForward: false),
            _ => false
        };

        // if (handled && focusedControl != null) {
        //     Draw();
        // }
    }

    protected virtual void OnResize()
    {
        foreach (Control control in Controls) {
            control.Resize();
        }
    }

    protected virtual void OnUnload()
    {
        foreach (Control control in Controls) {
            control.Unload();
        }
    }

    internal Control? Parent { get; set; }
    
    protected bool ProcessTabKey(bool lookForward)
    {
        // if (focusedControl == null) {
        //     focusedControl = SelectFirstControl(this, lookForward);
        //     return true;
        // }
        //
        // Control? nextControl = SelectNextControl(focusedControl, lookForward);
        //
        // if (nextControl != null) {
        //     focusedControl?.LostFocus();
        //     focusedControl = nextControl;
        //     focusedControl.GotFocus();
        // }

        return true;
    }

    public static bool RedrawEnabled { get; set; } = true;
    
    internal void RemoveControlAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, controls.Count, nameof(index));
        
        controls.RemoveAt(index);
    }

    internal void RemoveControl(Control control)
    {
        int index = IndexOfControl(control);
        
        if (index != -1) {
            RemoveControlAt(index);
        }
    }

    public void Resize()
    {
        if (Visible) {
            OnResize();
        }
    }

    protected Control? SelectFirstControl(Control currentControl, bool lookForward)
    {
        if (currentControl.Controls.Count == 0 && currentControl.CanFocus) {
            return currentControl;
        }

        List<Control> orderedControls = GetOrderedTabControls(currentControl.Controls, lookForward).ToList();

        foreach (Control childControl in orderedControls) {
            Control? selectableControl = SelectFirstControl(childControl, lookForward);
            
            if (selectableControl != null) {
                return selectableControl;
            }
        }

        return null;
    }

    protected Control? SelectNextControl(Control? currentControl, bool lookForward)
    {
        if (currentControl == null) {
            return lookForward 
                ? GetOrderedTabControls(Controls, lookForward: true).FirstOrDefault() 
                : GetOrderedTabControls(Controls, lookForward: false).FirstOrDefault();
        }
        
        return SelectNextControlRecursive(currentControl, lookForward);
    }

    private Control? SelectNextControlInContainer(
        Control container, 
        Control? currentControl, 
        bool lookForward)
    {
        List<Control> orderedControls = GetOrderedTabControls(container.Controls, lookForward).ToList();
        
        if (orderedControls.Count == 0) {
            return null;
        }

        if (currentControl != null) {
            int currentIndex = orderedControls.IndexOf(currentControl);

            if (currentIndex != -1) {
                int nextIndex = currentIndex + (lookForward ? 1 : -1);

                if (nextIndex >= 0 && nextIndex < orderedControls.Count) {
                    return orderedControls[nextIndex];
                }

                // We hit the boundary (first or last control). Return null to trigger recursion up the hierarchy.
                return null;
            }
        }

        // If the current control wasn't found in this container's children, 
        // it means we are just starting or coming from outside. Return the first/last child.
        return lookForward ? orderedControls.FirstOrDefault() : orderedControls.LastOrDefault();
    }
    
    private Control? SelectNextControlRecursive(Control currentControl, bool lookForward)
    {
        Control? container = currentControl.Parent;
        
        if (container == null) {
            return null;
        }

        Control? nextControl = SelectNextControlInContainer(
            container, 
            currentControl, 
            lookForward);

        if (nextControl != null) {
            if (nextControl.Controls.Count > 0 && nextControl.TabStop) {
                Control? firstOrLastChild = SelectNextControlInContainer(
                    nextControl, 
                    null, 
                    lookForward);
                
                return firstOrLastChild ?? nextControl; 
            }
            
            return nextControl;
        }

        // Boundary hit (hit the end/start of the container's children list).
        if (container == this) {
            // We hit the boundary of the top-level screen/form. Stop recursion.
            return null;
        }

        // Recursively call the function on the container itself to jump to the next control 
        // after the container in the parent's tab order.
        return SelectNextControlRecursive(container, lookForward);
    }

    public void SetFocus()
    {
        Screen? parent = GetParentScreen();

        if (parent == null) {
            return;
        }
        
        parent.FocusInternal(this);
    }
    
    public uint TabIndex { get; set; } = 0;
    
    public bool TabStop { get; set; } = false;
    
    protected ISystemTerminal Terminal => terminal;
    
    public void Unload() => OnUnload();

    public virtual bool Visible { get; set; } = true;
    
    public int Width { get; set; } = 0;

    public int X { get; set; } = 0;
    
    public int Y { get; set; } = 0;
}
