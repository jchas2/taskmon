using System.Drawing;
using Task.Monitor.System.Controls;
using Task.Monitor.System.Controls.InputBox;
using Task.Monitor.System.Controls.MessageBox;

namespace Task.Monitor.System.Screens;

public partial class Screen : Control
{
    private readonly MessageBox messageBox;
    private readonly InputBox inputBox;
    
    private Control? focusedControl;
    
    Action? onMessageBoxResult;
    Action<string, InputBoxResult>? onInputBoxResult;

    private const int MessageBoxWidth = 48;
    private const int MessageBoxHeight = 11;
    
    private const int InputBoxWidth = 48;

    public Screen(ISystemTerminal systemTerminal) : base(systemTerminal)
    {
        messageBox = new MessageBox(systemTerminal) {
            Width = MessageBoxWidth,
            Height = MessageBoxHeight,
            Visible = false
        };

        inputBox = new InputBox(systemTerminal) {
            Width = MessageBoxWidth,
            Height = 1,
            Visible = false
        };
    }

    public override Color BackgroundColour
    {
        get => base.BackgroundColour;
        set {
            base.BackgroundColour = value;
            messageBox.BackgroundColour = value;
            inputBox.BackgroundColour = value;
        }
    }

    public void Close()
    {
        Unload();
        IsActive = false;
    }

    public bool CursorVisible { get; set; } = true;

    private void Focus()
    {
        if (focusedControl != null) {
            focusedControl.Focused = false;    
        }
        
        focusedControl = SelectFirstControl(currentControl: this, lookForward: true);
        
        if (focusedControl != null) {
            focusedControl.Focused = true;
        }
    }

    internal void FocusInternal(Control control)
    {
        if (focusedControl == control) {
            return;
        }
        
        if (focusedControl != null) {
            focusedControl.Focused = false;
            focusedControl.LostFocus();
        }

        focusedControl = control;
        focusedControl.Focused = true;
        focusedControl.GotFocus();
    }
    
    public override Color ForegroundColour
    {
        get => base.ForegroundColour;
        set {
            base.ForegroundColour = value;
            messageBox.ForegroundColour = value;
            inputBox.ForegroundColour = value;
        }
    }

    private bool IsActive { get; set; } = false;

    protected override void OnClear()
    {
        base.OnClear();
        
        messageBox.Clear();
        inputBox.Clear();
    }

    protected override void OnDraw()
    {
        base.OnDraw();

        if (messageBox.Visible) {
            messageBox.Draw();
        }

        if (inputBox.Visible) {
            inputBox.Draw();
        }
    }

    protected override void OnKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        base.OnKeyPressed(keyInfo, ref handled);

        if (handled) {
            return;
        }
        
        if (messageBox.Visible) {
            OnMessageBoxKeyPressed(keyInfo, ref handled);
        }
        else if (inputBox.Visible) {
            OnInputBoxKeyPressed(keyInfo, ref handled);
        }
    }

    protected override void OnLoad()
    {
        messageBox.BackgroundColour = BackgroundColour;
        messageBox.ForegroundColour = ForegroundColour;
        messageBox.Load();

        inputBox.BackgroundColour = BackgroundColour;
        inputBox.ForegroundColour = ForegroundColour;
        inputBox.Load();

        focusedControl = SelectNextControl(null, lookForward: true);
        
        base.OnLoad();
    }

    private void OnInputBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        inputBox.KeyPressed(keyInfo, ref handled);

        if (inputBox.Result == InputBoxResult.None) {
            return;
        }

        Control.RedrawEnabled = true;
        inputBox.Visible = false;

        onInputBoxResult?.Invoke(inputBox.Text, inputBox.Result);

        Draw();
    }
    
    private void OnMessageBoxKeyPressed(ConsoleKeyInfo keyInfo, ref bool handled)
    {
        messageBox.KeyPressed(keyInfo, ref handled);

        if (messageBox.Result == MessageBoxResult.None) {
            return;
        }

        Control.RedrawEnabled = true;
        messageBox.Visible = false;

        if (messageBox.Result == MessageBoxResult.Ok) {
            onMessageBoxResult?.Invoke();
        }

        Clear();
        Draw();
    }
    
    protected override void OnResize()
    {
        messageBox.X = X + (Width / 2 - messageBox.Width / 2);
        messageBox.Y = Y + (Height / 2 - messageBox.Height / 2);
        messageBox.Resize();
        
        base.OnResize();
    }
    
    protected virtual void OnShown() { }

    protected override void OnUnload()
    {
        base.OnUnload();
        
        messageBox.Unload();
        inputBox.Unload();
    }

    public void Show()
    {
        Load();
        Clear();
        Resize();
        Focus();
        Draw();
        OnShown();
        IsActive = true;
    }

    public void ShowInputBox(
        int x, 
        int y,
        int width,
        string title, 
        Action<string, InputBoxResult> onInputResult)
    {
        Control.RedrawEnabled = false;
        onInputBoxResult = onInputResult;

        inputBox.Visible = true;
        inputBox.X = x;
        inputBox.Y = y;
        inputBox.Width = width;
        inputBox.Title = title;
        
        inputBox.ShowInputBox();
    }

    public void ShowMessageBox(
        string title,
        string text,
        MessageBoxButtons buttons,
        Action action,
        int width = MessageBoxWidth)
    {
        RedrawEnabled = false;
        onMessageBoxResult = action;

        messageBox.Visible = true;
        messageBox.Buttons = buttons;
        messageBox.Text = text;
        messageBox.Title = title;
        messageBox.Width = width;
        
        messageBox.ShowMessageBox();
    }
}
